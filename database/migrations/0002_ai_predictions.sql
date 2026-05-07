-- =============================================================================
-- Migration 0002 — AI predictions + model version registry
-- =============================================================================
-- Adds two tables and two ENUM types for the AI pipeline.
--
--   model_versions   — global registry of model deployments (no RLS, platform-wide)
--   ai_predictions   — append-only audit log of every AI inference (RLS, tenant-scoped)
--
-- Source of truth for prompt templates is git (ai-service/app/pipeline/prompts/).
-- This table stores the *executed* prompt text + raw response for observability.
-- =============================================================================


-- ----------------------------------------------------------------------------
-- 1. ENUM types
-- ----------------------------------------------------------------------------

-- Stages of the AI pipeline (CLAUDE.md §The AI Pipeline).
-- Append-only — adding a new stage requires ALTER TYPE … ADD VALUE.
CREATE TYPE ai_stage AS ENUM (
    'transcription',     -- 1. voice → text
    'sentiment',         -- 2. sentiment + urgency
    'classifier',        -- 3. category + confidence
    'priority',          -- 4. priority score 0-100
    'router',            -- 5. department routing
    'matcher',           -- 6. staff member matching
    'recommender',       -- 7. solution recommender
    'satisfaction',      -- 8. satisfaction predictor
    'chatbot',           -- 9. chatbot turn
    'learning'           -- 10. continuous-learning event
);

-- Model providers — one prediction has exactly one provider.
-- 'rule_based' is the deterministic fallback per CLAUDE.md §AI Engineering #5.
CREATE TYPE ai_provider AS ENUM (
    'openai',
    'anthropic',
    'rule_based',
    'custom'
);


-- ----------------------------------------------------------------------------
-- 2. model_versions — global registry, NO RLS (platform-wide concern)
-- ----------------------------------------------------------------------------

CREATE TABLE model_versions (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    -- Logical model identity (e.g., 'sentiment-classifier', 'priority-ranker')
    name            TEXT NOT NULL,
    -- Semver string (CLAUDE.md §AI Engineering #8 — versions are immutable)
    version         TEXT NOT NULL,
    provider        ai_provider NOT NULL,
    -- Provider-specific model identifier
    -- e.g., 'gpt-4o-mini-2024-07-18', 'claude-haiku-4-5-20251001', NULL for rule_based
    model_id        TEXT,
    -- Pointer to the prompt revision in git (ai-service/app/pipeline/prompts/).
    -- e.g., 'sentiment-v3'. The prompt body stays in source control.
    prompt_version  TEXT,
    -- Rollout lifecycle (CLAUDE.md §Deployment — shadow → canary → full)
    deployed_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    shadow_until    TIMESTAMPTZ,                                -- NULL = past shadow
    canary_percent  INT NOT NULL DEFAULT 0
                    CHECK (canary_percent BETWEEN 0 AND 100),
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    -- Cost tracking (CLAUDE.md §AI Engineering #10 — < USD 0.01 per pipeline run)
    cost_per_1k_input_tokens   NUMERIC(10, 6),
    cost_per_1k_output_tokens  NUMERIC(10, 6),
    metadata        JSONB NOT NULL DEFAULT '{}'::jsonb,
    notes           TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (name, version)
);

CREATE INDEX idx_model_versions_active
    ON model_versions (name, is_active) WHERE is_active;
CREATE INDEX idx_model_versions_canary
    ON model_versions (canary_percent) WHERE canary_percent > 0 AND is_active;


-- ----------------------------------------------------------------------------
-- 3. ai_predictions — append-only audit log, RLS, tenant-scoped
-- ----------------------------------------------------------------------------

CREATE TABLE ai_predictions (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    business_id     UUID NOT NULL REFERENCES businesses(id) ON DELETE CASCADE,
    -- Subject of the prediction (≥1 required, see CHECK below)
    issue_id        UUID REFERENCES issues(id) ON DELETE CASCADE,
    feedback_id     UUID REFERENCES feedback(id) ON DELETE CASCADE,
    session_id      UUID REFERENCES feedback_sessions(id) ON DELETE CASCADE,
    -- Pipeline stage
    stage           ai_stage NOT NULL,
    -- Cache key — SHA256 hex of normalized input. Used for Redis cache lookup
    -- (CLAUDE.md §Performance) and dedup during retraining.
    input_hash      TEXT NOT NULL,
    -- Structured output the application consumes
    output_json     JSONB NOT NULL,
    -- Human-readable explanation (CLAUDE.md §AI Engineering #4 — no hidden decisions)
    explanation     TEXT,
    -- Execution observability — the *rendered* prompt actually sent + raw response.
    -- Source of truth for the prompt template is git; this captures one execution.
    prompt_text     TEXT,
    response_text   TEXT,
    -- Conservative default — assume rendered prompt contains guest input until
    -- the application stamps this FALSE. Training-corpus extraction filters on
    -- this column to enforce CLAUDE.md §AI Engineering #7 (no PII in training).
    contains_pii    BOOLEAN NOT NULL DEFAULT TRUE,
    -- Model lineage — denormalized for audit even if model_versions row is deleted.
    model_version_id  UUID REFERENCES model_versions(id) ON DELETE SET NULL,
    model_version     TEXT NOT NULL,
    prompt_version    TEXT,
    provider          ai_provider NOT NULL,
    -- Confidence ∈ [0, 1]. NULL when the stage doesn't produce a confidence score.
    confidence      NUMERIC(5, 4)
                    CHECK (confidence IS NULL OR (confidence >= 0 AND confidence <= 1)),
    latency_ms      INT NOT NULL CHECK (latency_ms >= 0),
    -- USD cost of this single inference (CLAUDE.md §AI Engineering #10)
    cost_usd        NUMERIC(10, 6),
    -- Fallback flag (CLAUDE.md §AI Engineering #5 — graceful degradation, never silent)
    ai_fallback     BOOLEAN NOT NULL DEFAULT FALSE,
    fallback_reason TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    -- At least one subject must be set
    CONSTRAINT subject_required CHECK (
        issue_id IS NOT NULL OR feedback_id IS NOT NULL OR session_id IS NOT NULL
    )
);

-- Per-issue stage lookup (e.g., "show me all predictions for issue X")
CREATE INDEX idx_ai_predictions_issue
    ON ai_predictions (issue_id, stage, created_at DESC) WHERE issue_id IS NOT NULL;

-- Per-feedback (sentiment / classifier run before issue exists)
CREATE INDEX idx_ai_predictions_feedback
    ON ai_predictions (feedback_id, stage, created_at DESC) WHERE feedback_id IS NOT NULL;

-- Per-session (chatbot turns)
CREATE INDEX idx_ai_predictions_session
    ON ai_predictions (session_id, stage, created_at DESC) WHERE session_id IS NOT NULL;

-- Tenant-scoped recent (analytics dashboards)
CREATE INDEX idx_ai_predictions_business_recent
    ON ai_predictions (business_id, created_at DESC);

-- Per-stage perf (latency tracking for the AI accuracy dashboard)
CREATE INDEX idx_ai_predictions_stage_recent
    ON ai_predictions (stage, created_at DESC);

-- Per-model-version (A/B comparison: gpt-4o-mini vs Haiku)
CREATE INDEX idx_ai_predictions_model_version
    ON ai_predictions (model_version, created_at DESC);

-- Cache lookups
CREATE INDEX idx_ai_predictions_input_hash
    ON ai_predictions (input_hash);

-- Fallback monitoring (CLAUDE.md alert threshold: > 10% over 1 hour)
CREATE INDEX idx_ai_predictions_fallback
    ON ai_predictions (business_id, created_at DESC) WHERE ai_fallback;


-- ----------------------------------------------------------------------------
-- 4. RLS on ai_predictions (model_versions stays unrestricted)
-- ----------------------------------------------------------------------------

ALTER TABLE ai_predictions ENABLE ROW LEVEL SECURITY;
ALTER TABLE ai_predictions FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON ai_predictions
    USING       (is_platform_admin() OR business_id = current_business_id())
    WITH CHECK  (is_platform_admin() OR business_id = current_business_id());


-- =============================================================================
-- END OF MIGRATION 0002
-- =============================================================================
