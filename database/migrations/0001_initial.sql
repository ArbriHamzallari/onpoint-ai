-- =============================================================================
-- OnPoint AI — Production PostgreSQL Schema (v1)
-- Target: PostgreSQL 16+
-- Strategy: Single database, single schema, tenant-discriminator (business_id)
--           with Row-Level Security (RLS) for isolation.
-- =============================================================================

-- ----------------------------------------------------------------------------
-- 0. Extensions
-- ----------------------------------------------------------------------------
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "citext";        -- case-insensitive text (emails)
CREATE EXTENSION IF NOT EXISTS "pg_trgm";       -- text similarity (fraud detection)
CREATE EXTENSION IF NOT EXISTS "btree_gin";     -- composite GIN indexes


-- ----------------------------------------------------------------------------
-- 1. Tenancy & business setup
-- ----------------------------------------------------------------------------

CREATE TYPE business_type AS ENUM ('hotel', 'restaurant', 'retail', 'service', 'healthcare', 'other');
CREATE TYPE business_plan AS ENUM ('trial', 'starter', 'growth', 'enterprise');

CREATE TABLE businesses (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    slug            CITEXT UNIQUE NOT NULL,
    name            TEXT NOT NULL,
    type            business_type NOT NULL DEFAULT 'other',
    plan            business_plan NOT NULL DEFAULT 'trial',
    timezone        TEXT NOT NULL DEFAULT 'UTC',
    locale          TEXT NOT NULL DEFAULT 'en-US',
    logo_url        TEXT,
    -- Public review redirects (Google Place ID, TripAdvisor URL, etc.)
    public_review_links JSONB NOT NULL DEFAULT '{}'::jsonb,
    -- Earning rules JSON (see 06-POINTS-REWARDS.md)
    earning_rules   JSONB NOT NULL DEFAULT '{}'::jsonb,
    -- Misc settings
    settings        JSONB NOT NULL DEFAULT '{}'::jsonb,
    trial_ends_at   TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at      TIMESTAMPTZ
);

CREATE INDEX idx_businesses_slug ON businesses (slug) WHERE deleted_at IS NULL;
CREATE INDEX idx_businesses_active ON businesses (id) WHERE deleted_at IS NULL;


CREATE TABLE departments (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    business_id     UUID NOT NULL REFERENCES businesses(id) ON DELETE CASCADE,
    name            TEXT NOT NULL,
    description     TEXT,
    icon            TEXT,
    -- Categories from feedback that route here
    handles_categories TEXT[] NOT NULL DEFAULT '{}',
    sla_minutes     INT NOT NULL DEFAULT 60,
    sort_order      INT NOT NULL DEFAULT 0,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (business_id, name)
);

CREATE INDEX idx_departments_business ON departments (business_id) WHERE is_active;


CREATE TYPE location_type AS ENUM ('room', 'table', 'public_area', 'department', 'service_point', 'other');

CREATE TABLE locations (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    business_id     UUID NOT NULL REFERENCES businesses(id) ON DELETE CASCADE,
    name            TEXT NOT NULL,         -- "Room 204", "Table 12"
    label           TEXT,                  -- "Deluxe", "Patio"
    type            location_type NOT NULL DEFAULT 'other',
    -- Stable short code embedded in QR URL: onpoint.ai/r/{short_code}
    short_code      TEXT UNIQUE NOT NULL,
    -- Optional grouping (e.g., "Floor 2" → many rooms)
    parent_id       UUID REFERENCES locations(id) ON DELETE SET NULL,
    metadata        JSONB NOT NULL DEFAULT '{}'::jsonb,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at      TIMESTAMPTZ,
    UNIQUE (business_id, name)
);

CREATE INDEX idx_locations_business ON locations (business_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_locations_short_code ON locations (short_code) WHERE deleted_at IS NULL;


-- ----------------------------------------------------------------------------
-- 2. Identity (staff users + guest users + sessions)
-- ----------------------------------------------------------------------------

CREATE TYPE user_role AS ENUM ('platform_admin', 'owner', 'manager', 'staff');

-- Staff users (people who log in to manage businesses)
CREATE TABLE staff_users (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    email           CITEXT UNIQUE NOT NULL,
    password_hash   TEXT NOT NULL,
    full_name       TEXT NOT NULL,
    avatar_url      TEXT,
    -- 2FA
    totp_secret     TEXT,                  -- encrypted at rest, key in Key Vault
    is_2fa_enabled  BOOLEAN NOT NULL DEFAULT FALSE,
    -- Account state
    is_email_verified BOOLEAN NOT NULL DEFAULT FALSE,
    email_verified_at TIMESTAMPTZ,
    last_login_at   TIMESTAMPTZ,
    failed_login_count INT NOT NULL DEFAULT 0,
    locked_until    TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at      TIMESTAMPTZ
);

CREATE INDEX idx_staff_users_email ON staff_users (email) WHERE deleted_at IS NULL;


-- Membership: which staff_users belong to which businesses, with what role
CREATE TABLE business_memberships (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    business_id     UUID NOT NULL REFERENCES businesses(id) ON DELETE CASCADE,
    staff_user_id   UUID NOT NULL REFERENCES staff_users(id) ON DELETE CASCADE,
    role            user_role NOT NULL,
    -- Department restrictions for staff role (NULL = all depts)
    department_ids  UUID[],
    invited_by      UUID REFERENCES staff_users(id),
    invitation_accepted_at TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (business_id, staff_user_id)
);

CREATE INDEX idx_memberships_user ON business_memberships (staff_user_id);
CREATE INDEX idx_memberships_business ON business_memberships (business_id);


-- Guest user accounts (created lazily, only when a guest opts to save points)
CREATE TABLE guest_users (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    email           CITEXT UNIQUE,
    phone           TEXT UNIQUE,
    -- Either email OR phone must be present
    CONSTRAINT contact_required CHECK (email IS NOT NULL OR phone IS NOT NULL),
    full_name       TEXT,
    is_email_verified BOOLEAN NOT NULL DEFAULT FALSE,
    is_phone_verified BOOLEAN NOT NULL DEFAULT FALSE,
    -- Locale preference
    locale          TEXT,
    -- Anti-fraud: flagged accounts
    fraud_score     INT NOT NULL DEFAULT 0,
    is_blocked      BOOLEAN NOT NULL DEFAULT FALSE,
    blocked_reason  TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at      TIMESTAMPTZ,
    -- For GDPR erasure: when set, all PII has been anonymized
    anonymized_at   TIMESTAMPTZ
);

CREATE INDEX idx_guest_users_email ON guest_users (email) WHERE email IS NOT NULL AND deleted_at IS NULL;
CREATE INDEX idx_guest_users_phone ON guest_users (phone) WHERE phone IS NOT NULL AND deleted_at IS NULL;


-- A "feedback session" is the unit of guest interaction.
-- Created when QR is scanned. Anonymous by default; may later be linked to guest_users.
CREATE TABLE feedback_sessions (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    business_id     UUID NOT NULL REFERENCES businesses(id) ON DELETE CASCADE,
    location_id     UUID REFERENCES locations(id) ON DELETE SET NULL,
    -- Linked guest account (NULL = anonymous)
    guest_user_id   UUID REFERENCES guest_users(id) ON DELETE SET NULL,
    -- Anti-fraud signals (collected at session creation)
    device_fingerprint_hash  TEXT,
    ip_hash                  TEXT,    -- SHA256(ip + daily_salt) for privacy
    user_agent               TEXT,
    geo_country              TEXT,
    geo_region               TEXT,
    -- Session lifecycle
    started_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_active_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at      TIMESTAMPTZ NOT NULL DEFAULT (now() + INTERVAL '24 hours'),
    -- Computed metrics for fraud
    fraud_score     INT NOT NULL DEFAULT 0
);

CREATE INDEX idx_sessions_business ON feedback_sessions (business_id, started_at DESC);
CREATE INDEX idx_sessions_guest ON feedback_sessions (guest_user_id) WHERE guest_user_id IS NOT NULL;
CREATE INDEX idx_sessions_fingerprint ON feedback_sessions (device_fingerprint_hash) WHERE device_fingerprint_hash IS NOT NULL;
CREATE INDEX idx_sessions_ip_recent ON feedback_sessions (ip_hash, started_at DESC) WHERE ip_hash IS NOT NULL;


-- ----------------------------------------------------------------------------
-- 3. Feedback core
-- ----------------------------------------------------------------------------

CREATE TYPE feedback_sentiment AS ENUM ('positive', 'neutral', 'negative', 'unknown');
CREATE TYPE feedback_severity  AS ENUM ('low', 'medium', 'high', 'urgent', 'unknown');

CREATE TABLE feedback (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    business_id     UUID NOT NULL REFERENCES businesses(id) ON DELETE CASCADE,
    session_id      UUID NOT NULL REFERENCES feedback_sessions(id) ON DELETE CASCADE,
    location_id     UUID REFERENCES locations(id) ON DELETE SET NULL,
    -- Core fields
    rating          SMALLINT NOT NULL CHECK (rating BETWEEN 1 AND 5),
    comment         TEXT,
    category_hint   TEXT,                  -- user-selected ("Room", "Service")
    -- AI classification (filled by Hangfire job)
    classification_status TEXT NOT NULL DEFAULT 'pending'
        CHECK (classification_status IN ('pending', 'completed', 'failed', 'skipped')),
    sentiment       feedback_sentiment,
    categories      TEXT[],                -- AI-detected
    severity        feedback_severity,
    routed_to_dept_id UUID REFERENCES departments(id) ON DELETE SET NULL,
    ai_metadata     JSONB,                 -- raw model output for debugging
    -- Output decision: was this redirected to public review?
    redirected_to_public BOOLEAN NOT NULL DEFAULT FALSE,
    redirect_target TEXT,                  -- "google" | "tripadvisor" | etc.
    -- PII handling
    contains_pii    BOOLEAN NOT NULL DEFAULT FALSE,
    -- Timestamps
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_feedback_business_created ON feedback (business_id, created_at DESC);
CREATE INDEX idx_feedback_session ON feedback (session_id);
CREATE INDEX idx_feedback_classification_pending ON feedback (created_at) WHERE classification_status = 'pending';
CREATE INDEX idx_feedback_negative_recent ON feedback (business_id, created_at DESC)
    WHERE sentiment = 'negative';


-- ----------------------------------------------------------------------------
-- 4. Issues (negative feedback gets promoted to issue)
-- ----------------------------------------------------------------------------

CREATE TYPE issue_status AS ENUM ('open', 'assigned', 'in_progress', 'resolved', 'cancelled');
CREATE TYPE issue_priority AS ENUM ('low', 'medium', 'high', 'urgent');

CREATE TABLE issues (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    business_id     UUID NOT NULL REFERENCES businesses(id) ON DELETE CASCADE,
    feedback_id     UUID NOT NULL REFERENCES feedback(id) ON DELETE CASCADE,
    session_id      UUID NOT NULL REFERENCES feedback_sessions(id) ON DELETE CASCADE,
    location_id     UUID REFERENCES locations(id) ON DELETE SET NULL,
    department_id   UUID REFERENCES departments(id) ON DELETE SET NULL,
    assigned_to     UUID REFERENCES staff_users(id) ON DELETE SET NULL,
    -- Display data (denormalized for fast feed rendering)
    title           TEXT NOT NULL,         -- AI-generated short summary
    description     TEXT,                  -- the original comment
    status          issue_status NOT NULL DEFAULT 'open',
    priority        issue_priority NOT NULL DEFAULT 'medium',
    -- Resolution
    resolution_note TEXT,
    resolved_by     UUID REFERENCES staff_users(id) ON DELETE SET NULL,
    resolved_at     TIMESTAMPTZ,
    guest_confirmed_resolution BOOLEAN,
    guest_confirmed_at TIMESTAMPTZ,
    guest_post_resolution_rating SMALLINT CHECK (guest_post_resolution_rating BETWEEN 1 AND 5),
    -- SLA tracking
    sla_breach_at   TIMESTAMPTZ,
    sla_breached    BOOLEAN NOT NULL DEFAULT FALSE,
    -- Timestamps
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    first_response_at TIMESTAMPTZ
);

CREATE INDEX idx_issues_business_status ON issues (business_id, status, created_at DESC);
CREATE INDEX idx_issues_dept_open ON issues (department_id, status) WHERE status IN ('open', 'assigned', 'in_progress');
CREATE INDEX idx_issues_assigned ON issues (assigned_to, status) WHERE assigned_to IS NOT NULL;
CREATE INDEX idx_issues_session ON issues (session_id);
CREATE INDEX idx_issues_sla_check ON issues (sla_breach_at) WHERE NOT sla_breached AND status NOT IN ('resolved', 'cancelled');


-- Internal notes/comments on issues (staff-only)
CREATE TABLE issue_comments (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    business_id     UUID NOT NULL REFERENCES businesses(id) ON DELETE CASCADE,
    issue_id        UUID NOT NULL REFERENCES issues(id) ON DELETE CASCADE,
    author_id       UUID NOT NULL REFERENCES staff_users(id) ON DELETE CASCADE,
    body            TEXT NOT NULL,
    is_internal     BOOLEAN NOT NULL DEFAULT TRUE,  -- false = visible to guest
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_issue_comments_issue ON issue_comments (issue_id, created_at);


-- Audit log for issue status changes
CREATE TABLE issue_events (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    business_id     UUID NOT NULL REFERENCES businesses(id) ON DELETE CASCADE,
    issue_id        UUID NOT NULL REFERENCES issues(id) ON DELETE CASCADE,
    event_type      TEXT NOT NULL,         -- 'created' | 'assigned' | 'status_changed' | 'resolved' | 'commented'
    actor_type      TEXT NOT NULL,         -- 'staff' | 'guest' | 'system'
    actor_id        UUID,
    payload         JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_issue_events_issue ON issue_events (issue_id, created_at);


-- ----------------------------------------------------------------------------
-- 5. Points & rewards (the financial-grade ledger)
-- ----------------------------------------------------------------------------

CREATE TYPE points_entry_status AS ENUM ('confirmed', 'pending_review', 'reversed', 'expired');

-- The ledger: every point ever awarded or spent.
-- A user's balance is SUM(amount) WHERE status = 'confirmed' AND user_id = ?
CREATE TABLE points_ledger (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    -- Either guest_user_id or session_id is set.
    -- Anonymous earnings live on session_id; they "merge" to guest_user_id at account creation.
    guest_user_id   UUID REFERENCES guest_users(id) ON DELETE SET NULL,
    session_id      UUID REFERENCES feedback_sessions(id) ON DELETE SET NULL,
    business_id     UUID NOT NULL REFERENCES businesses(id) ON DELETE CASCADE,
    -- Amount: positive = earned, negative = spent
    amount          INT NOT NULL CHECK (amount != 0),
    reason          TEXT NOT NULL,         -- 'feedback_submitted' | 'redemption' | 'reversal' | etc.
    -- Source linking
    feedback_id     UUID REFERENCES feedback(id) ON DELETE SET NULL,
    issue_id        UUID REFERENCES issues(id) ON DELETE SET NULL,
    redemption_id   UUID,                  -- forward ref, see below
    -- State machine
    status          points_entry_status NOT NULL DEFAULT 'confirmed',
    -- Anti-fraud
    fraud_score     INT NOT NULL DEFAULT 0,
    flagged         BOOLEAN NOT NULL DEFAULT FALSE,
    -- Expiration (per-business config can set this)
    expires_at      TIMESTAMPTZ,
    -- Reversal
    reversed_by_entry_id UUID REFERENCES points_ledger(id),
    reversed_reason TEXT,
    -- Timestamps
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    confirmed_at    TIMESTAMPTZ,
    -- Constraints
    CONSTRAINT subject_required CHECK (guest_user_id IS NOT NULL OR session_id IS NOT NULL)
);

-- Hot path: get a user's balance
CREATE INDEX idx_ledger_user_confirmed ON points_ledger (guest_user_id, business_id)
    WHERE status = 'confirmed' AND guest_user_id IS NOT NULL;
CREATE INDEX idx_ledger_session_confirmed ON points_ledger (session_id, business_id)
    WHERE status = 'confirmed' AND session_id IS NOT NULL;
-- Expiration scan
CREATE INDEX idx_ledger_expiring ON points_ledger (expires_at)
    WHERE expires_at IS NOT NULL AND status = 'confirmed';
-- Pending review
CREATE INDEX idx_ledger_pending ON points_ledger (business_id, created_at DESC)
    WHERE status = 'pending_review';


-- Reward catalog
CREATE TABLE rewards (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    business_id     UUID NOT NULL REFERENCES businesses(id) ON DELETE CASCADE,
    name            TEXT NOT NULL,
    description     TEXT,
    image_url       TEXT,
    cost_points     INT NOT NULL CHECK (cost_points > 0),
    -- NULL stock = unlimited
    stock           INT,
    -- Validity after redemption
    expiry_days     INT NOT NULL DEFAULT 30,
    terms           TEXT,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order      INT NOT NULL DEFAULT 0,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at      TIMESTAMPTZ,
    CONSTRAINT stock_non_negative CHECK (stock IS NULL OR stock >= 0)
);

CREATE INDEX idx_rewards_business_active ON rewards (business_id, sort_order)
    WHERE is_active AND deleted_at IS NULL;


CREATE TYPE redemption_status AS ENUM ('pending', 'claimed', 'expired', 'cancelled', 'refunded');

CREATE TABLE redemptions (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    business_id     UUID NOT NULL REFERENCES businesses(id) ON DELETE CASCADE,
    guest_user_id   UUID NOT NULL REFERENCES guest_users(id) ON DELETE CASCADE,
    reward_id       UUID NOT NULL REFERENCES rewards(id) ON DELETE RESTRICT,
    code            TEXT UNIQUE NOT NULL,  -- short, human-friendly: "ABC-1234"
    cost_points     INT NOT NULL,
    status          redemption_status NOT NULL DEFAULT 'pending',
    -- Linked ledger entry (the debit)
    ledger_entry_id UUID NOT NULL REFERENCES points_ledger(id),
    -- Lifecycle
    expires_at      TIMESTAMPTZ NOT NULL,
    claimed_at      TIMESTAMPTZ,
    claimed_by      UUID REFERENCES staff_users(id) ON DELETE SET NULL,
    cancelled_at    TIMESTAMPTZ,
    cancelled_reason TEXT,
    -- Created
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_redemptions_business_status ON redemptions (business_id, status, created_at DESC);
CREATE INDEX idx_redemptions_user ON redemptions (guest_user_id, created_at DESC);
CREATE INDEX idx_redemptions_code ON redemptions (code);
CREATE INDEX idx_redemptions_expiring ON redemptions (expires_at)
    WHERE status = 'pending';

-- Now we can add the FK from points_ledger.redemption_id
ALTER TABLE points_ledger
    ADD CONSTRAINT fk_ledger_redemption
    FOREIGN KEY (redemption_id) REFERENCES redemptions(id) ON DELETE SET NULL;


-- ----------------------------------------------------------------------------
-- 6. Anti-fraud signals (raw events, used by scorer)
-- ----------------------------------------------------------------------------

CREATE TABLE fraud_signals (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    business_id     UUID REFERENCES businesses(id) ON DELETE CASCADE,
    session_id      UUID REFERENCES feedback_sessions(id) ON DELETE CASCADE,
    guest_user_id   UUID REFERENCES guest_users(id) ON DELETE CASCADE,
    signal_type     TEXT NOT NULL,         -- 'velocity' | 'fingerprint_dup' | 'ip_cluster' | etc.
    weight          INT NOT NULL,
    details         JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_fraud_signals_session ON fraud_signals (session_id, created_at DESC);
CREATE INDEX idx_fraud_signals_user ON fraud_signals (guest_user_id, created_at DESC);
CREATE INDEX idx_fraud_signals_business_recent ON fraud_signals (business_id, created_at DESC);


-- ----------------------------------------------------------------------------
-- 7. Notifications (outbound to staff and guests)
-- ----------------------------------------------------------------------------

CREATE TYPE notification_channel AS ENUM ('in_app', 'email', 'sms', 'push', 'webhook');
CREATE TYPE notification_status AS ENUM ('pending', 'sent', 'delivered', 'failed', 'read');

CREATE TABLE notifications (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    business_id     UUID REFERENCES businesses(id) ON DELETE CASCADE,
    -- One of these is set
    staff_user_id   UUID REFERENCES staff_users(id) ON DELETE CASCADE,
    guest_user_id   UUID REFERENCES guest_users(id) ON DELETE CASCADE,
    session_id      UUID REFERENCES feedback_sessions(id) ON DELETE CASCADE,
    -- Content
    channel         notification_channel NOT NULL,
    type            TEXT NOT NULL,         -- 'issue.created' | 'issue.resolved' | 'points.earned' | etc.
    title           TEXT NOT NULL,
    body            TEXT,
    payload         JSONB NOT NULL DEFAULT '{}'::jsonb,
    -- Delivery
    status          notification_status NOT NULL DEFAULT 'pending',
    sent_at         TIMESTAMPTZ,
    read_at         TIMESTAMPTZ,
    error           TEXT,
    -- Timestamps
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_notif_staff_unread ON notifications (staff_user_id, created_at DESC)
    WHERE staff_user_id IS NOT NULL AND read_at IS NULL;
CREATE INDEX idx_notif_pending ON notifications (created_at) WHERE status = 'pending';


-- ----------------------------------------------------------------------------
-- 8. Audit log (everything that matters)
-- ----------------------------------------------------------------------------

CREATE TABLE audit_log (
    id              BIGSERIAL PRIMARY KEY,
    business_id     UUID REFERENCES businesses(id) ON DELETE SET NULL,
    actor_type      TEXT NOT NULL,         -- 'staff' | 'guest' | 'system' | 'admin'
    actor_id        UUID,
    action          TEXT NOT NULL,
    resource_type   TEXT NOT NULL,
    resource_id     UUID,
    ip_hash         TEXT,
    user_agent      TEXT,
    payload         JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_audit_business_recent ON audit_log (business_id, created_at DESC);
CREATE INDEX idx_audit_resource ON audit_log (resource_type, resource_id);


-- ----------------------------------------------------------------------------
-- 9. Updated_at triggers (DRY)
-- ----------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION set_updated_at() RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DO $$
DECLARE
    t TEXT;
BEGIN
    FOR t IN
        SELECT table_name FROM information_schema.columns
        WHERE column_name = 'updated_at' AND table_schema = 'public'
    LOOP
        EXECUTE format(
            'CREATE TRIGGER trg_%I_updated BEFORE UPDATE ON %I
             FOR EACH ROW EXECUTE FUNCTION set_updated_at();',
            t, t
        );
    END LOOP;
END $$;


-- ----------------------------------------------------------------------------
-- 10. ROW-LEVEL SECURITY (the heart of multi-tenancy)
-- ----------------------------------------------------------------------------

-- Application sets `app.current_business_id` per connection/transaction.
-- Platform admins set `app.is_platform_admin = true` to bypass.

CREATE OR REPLACE FUNCTION current_business_id() RETURNS UUID AS $$
BEGIN
    RETURN NULLIF(current_setting('app.current_business_id', TRUE), '')::UUID;
EXCEPTION WHEN OTHERS THEN
    RETURN NULL;
END;
$$ LANGUAGE plpgsql STABLE;

CREATE OR REPLACE FUNCTION is_platform_admin() RETURNS BOOLEAN AS $$
BEGIN
    RETURN COALESCE(current_setting('app.is_platform_admin', TRUE) = 'true', FALSE);
EXCEPTION WHEN OTHERS THEN
    RETURN FALSE;
END;
$$ LANGUAGE plpgsql STABLE;

-- Apply RLS to every tenant-scoped table.
DO $$
DECLARE
    t TEXT;
    tenant_tables TEXT[] := ARRAY[
        'departments', 'locations', 'business_memberships',
        'feedback_sessions', 'feedback', 'issues', 'issue_comments', 'issue_events',
        'points_ledger', 'rewards', 'redemptions', 'fraud_signals', 'notifications'
    ];
BEGIN
    FOREACH t IN ARRAY tenant_tables LOOP
        EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY;', t);
        EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY;', t);
        EXECUTE format(
            'CREATE POLICY tenant_isolation ON %I
             USING (is_platform_admin() OR business_id = current_business_id())
             WITH CHECK (is_platform_admin() OR business_id = current_business_id());',
            t
        );
    END LOOP;
END $$;


-- Note: `businesses`, `staff_users`, `guest_users` are NOT under RLS.
-- They are accessed via explicit application logic with proper auth checks.
-- Putting them under RLS would break login (you can't query "your" business
-- before you know which business you belong to).


-- ----------------------------------------------------------------------------
-- 11. Initial seed (development only — remove for production)
-- ----------------------------------------------------------------------------

-- A demo business for local development
-- INSERT INTO businesses (slug, name, type, plan)
-- VALUES ('oceanview', 'Oceanview Hotel', 'hotel', 'trial');

-- =============================================================================
-- END OF SCHEMA
-- =============================================================================
