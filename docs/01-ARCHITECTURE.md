# OnPoint AI — System Architecture (v1)

**Status:** Production blueprint, ready for build
**Stack:** ASP.NET Core 8 · PostgreSQL 16 · React 18 · SignalR · Azure
**Identity model:** Hybrid (anonymous-first, optional account at redemption)
**Target MVP:** 8–12 weeks

---

## 1. The shape of the system

OnPoint AI is **three apps + one backend + a few infrastructure services**:

```
┌─────────────────────────────────────────────────────────────────────┐
│                                                                     │
│   GUEST PWA                STAFF DASHBOARD          ADMIN CONSOLE   │
│   (React, mobile-first)    (React, desktop)        (React, tenant   │
│   No login required        Login required           management)     │
│                                                                     │
└──────────────┬──────────────────┬──────────────────────┬────────────┘
               │                  │                      │
               │ HTTPS / WSS      │ HTTPS / WSS          │ HTTPS / WSS
               ▼                  ▼                      ▼
┌─────────────────────────────────────────────────────────────────────┐
│                                                                     │
│              ASP.NET Core 8 API + SignalR Hubs                      │
│                                                                     │
│   ┌───────────┐  ┌──────────┐  ┌──────────┐  ┌───────────────┐     │
│   │ Feedback  │  │ Identity │  │ Rewards  │  │ AI Routing    │     │
│   │ Module    │  │ Module   │  │ Module   │  │ Module        │     │
│   └───────────┘  └──────────┘  └──────────┘  └───────────────┘     │
│                                                                     │
│   ┌───────────┐  ┌──────────┐  ┌──────────┐                        │
│   │ Tenant    │  │ Notif    │  │ Analytics│                        │
│   │ Module    │  │ Module   │  │ Module   │                        │
│   └───────────┘  └──────────┘  └──────────┘                        │
│                                                                     │
└──────┬──────────────┬─────────────┬──────────────┬──────────────────┘
       │              │             │              │
       ▼              ▼             ▼              ▼
┌────────────┐ ┌────────────┐ ┌──────────┐ ┌─────────────────┐
│ PostgreSQL │ │ Redis      │ │ Hangfire │ │ Azure SignalR   │
│ (primary)  │ │ (cache,    │ │ (jobs)   │ │ Service         │
│ + RLS      │ │  rate lim, │ │          │ │ (backplane)     │
│            │ │  pub/sub)  │ │          │ │                 │
└────────────┘ └────────────┘ └──────────┘ └─────────────────┘

       ┌───────────────────────────────────────────┐
       │  External: OpenAI / Azure OpenAI          │
       │            Twilio (SMS), SendGrid (email) │
       │            Azure Blob (uploads)           │
       └───────────────────────────────────────────┘
```

### Why this shape

- **One backend serving three frontends** — same data model, different UI for different actors. Don't split the backend until you have a scaling reason.
- **Modules, not microservices** — for the MVP, a modular monolith is the right call. You can extract services later when you have traffic that justifies it. Premature microservices is the #1 cause of dead SaaS startups.
- **PostgreSQL is the source of truth for everything** including points (atomic ledger). Redis is a cache/coordinator, never authoritative.
- **Azure SignalR Service** removes the WebSocket scaling problem from your plate entirely. You don't manage sticky sessions, connection limits, or backplane sync.

---

## 2. Multi-tenancy strategy

**Decision: shared database, shared schema, tenant-discriminator column with PostgreSQL Row-Level Security (RLS).**

Every tenant-owned table has a `business_id UUID NOT NULL` column. RLS policies enforce isolation at the database level — even a SQL injection that bypasses application logic cannot read another tenant's data because Postgres itself blocks it.

### How it works

1. Every API request is authenticated → resolves to a `business_id` (for staff) or a session (for guests, scoped to a single business by QR).
2. Before each query, the API sets a Postgres session variable: `SET LOCAL app.current_business_id = '<uuid>'`.
3. RLS policies on tenant tables read that variable and filter automatically.
4. Application code never has to remember to add `WHERE business_id = ?` — it's enforced by the database.

### Why not schema-per-tenant or database-per-tenant?

- **Schema-per-tenant** breaks migrations at ~50 tenants. You'd be running migrations 500 times.
- **Database-per-tenant** is great for enterprise/compliance but kills your unit economics until you're charging $500+/month per business.
- **Row-level with RLS** scales to ~10,000 tenants on one Postgres instance comfortably. By then you have enterprise customers and can move them to dedicated DBs.

### Tenant identity in JWT

Staff/admin JWTs include:
- `sub` (user id)
- `business_id` (current tenant context)
- `role` (Owner | Manager | Staff | Admin)
- `dept_ids` (departments this user can act on, for staff)

Guest sessions don't use JWT — they use a signed, short-lived session token (see Identity section).

---

## 3. Identity model (hybrid)

The single most important UX decision in this app.

### Three actor types

| Actor | Auth method | Persistence | Purpose |
|---|---|---|---|
| **Guest** | Anonymous device session, optionally upgraded to account | Device fingerprint + cookie + optional email | Submit feedback, track issue, claim rewards |
| **Staff** | Email + password (or SSO) → JWT | Full account | Resolve issues, manage operations |
| **Platform admin** | Email + password + 2FA → JWT | Full account, separate auth tenant | Manage businesses, billing, support |

### Guest identity flow

```
Scan QR
  │
  ▼
GET /r/{shortCode}
  │
  ├─ Server creates feedback_session (anonymous)
  │  Stores: device_fingerprint, ip_hash, location_id, business_id
  │  Returns: session_token (HMAC-signed, 24h expiry)
  │
  ▼
Guest leaves feedback (still anonymous)
  │
  ▼
[If positive] → Public review redirect, +X points to anonymous session
[If negative] → Issue created, real-time staff notification
  │
  ▼
Issue resolved → Guest gets push (via session token)
  │
  ▼
[OPTIONAL] "You earned 50 points. Save them?"
  │
  ├─ User enters email/phone
  ├─ Server creates user_account, links all sessions with same fingerprint
  └─ Future visits: same email → same account → cross-business points
```

### Why this works

- **Guest never blocked** — feedback flows in 3 taps, no signup wall.
- **Account creation happens at the point of value** (reward redemption), where motivation is highest.
- **Anonymous fraud is bounded** — points only become useful once linked to a verified contact, and we re-validate at that moment.
- **One account, many businesses** — once a user has an account, all their future sessions across any OnPoint-enabled business roll up to that account.

### Device fingerprinting (for fraud, not identity)

We use FingerprintJS open-source on the client. Server stores a hash. This is **not** a primary key — it's one signal of many for fraud detection. A fingerprint match raises suspicion of duplicate submission; it doesn't prove identity.

---

## 4. Real-time architecture (SignalR)

### Hubs

Two hubs, clear responsibilities:

| Hub | Path | Who connects | Purpose |
|---|---|---|---|
| `StaffHub` | `/hubs/staff` | Authenticated staff (JWT) | Live issue feed, status updates, notifications |
| `GuestHub` | `/hubs/guest` | Guest session token | Status updates on their issue |

### Group strategy

- **Staff connections** join groups: `business:{id}`, `business:{id}:dept:{deptId}`, `business:{id}:user:{userId}`
- **Guest connections** join one group: `session:{sessionId}`

Server publishes to the group, Azure SignalR Service fans out. You never broadcast — always target the smallest group that needs the event.

### Events catalog

(Full payload spec is in `04-SIGNALR-EVENTS.md`. Summary here.)

**Server → Staff:**
- `feedback.created` — new feedback arrived
- `issue.created` — negative feedback became an issue
- `issue.assigned` — issue routed to a department/user
- `issue.status_changed` — open → in_progress → resolved
- `issue.commented` — internal note added
- `metrics.tick` — dashboard counter update (every 30s, batched)

**Server → Guest:**
- `issue.status_changed` — your issue's status updated
- `issue.resolved` — your issue is done, prompt for satisfaction
- `points.earned` — you got points (with reason)

**Client → Server:**
- `staff.start_typing` / `staff.stop_typing` (for shared issue notes)

### Backplane

In production, multiple API instances need to share SignalR connections. Azure SignalR Service handles this transparently — you write the code as if you have one server, Azure scales it to N. No Redis backplane needed for SignalR.

---

## 5. AI routing layer

### What the AI does

For each feedback submission, in this order:

1. **Sentiment** — positive | neutral | negative (confidence score)
2. **Category** — based on business's configured categories (cleanliness, service, food, etc.) — multi-label
3. **Severity** — low | medium | high | urgent
4. **Department routing** — which department should handle this, based on category mapping
5. **PII detection** — flag if guest included credit card / personal data → mask before storing

### Provider abstraction

```csharp
public interface IFeedbackClassifier
{
    Task<ClassificationResult> ClassifyAsync(
        string text,
        ClassificationContext context,
        CancellationToken ct);
}

// Implementations:
// - OpenAIClassifier (default)
// - AzureOpenAIClassifier (recommended for production — data residency)
// - AnthropicClassifier (alternative)
// - StubClassifier (for tests and local dev)
```

The provider is selected via DI based on config. **Never** import provider SDKs outside the implementation file.

### Where AI runs

- **NOT** in the request thread. AI calls are slow (1–3s) and unreliable.
- Feedback submission returns immediately with a "classification: pending" status.
- A Hangfire job picks up the feedback, classifies, updates the row, broadcasts the result via SignalR.
- This means the dashboard shows a feedback item as "classifying..." for 1–3 seconds, then it animates into its final category. Good UX, decoupled architecture.

### Cost control

- Cache classifications by hash of normalized text (lowercased, trimmed). Many guests write similar things ("room was dirty", "great service") — cache TTL 30 days.
- Bound max input length (500 chars).
- For sentiment-only on positive feedback (rating ≥ 4), use a much cheaper local heuristic — don't burn tokens on "Great stay!"

---

## 6. Points & rewards engine

### The ledger model

**Points are double-entry accounting, not a counter.**

Every change to a user's balance is a row in `points_ledger` with a positive or negative amount and a reason. The user's balance is `SUM(amount)` over their ledger entries. This is non-negotiable for:
- Auditability (where did these points come from?)
- Fraud reversal (we can claw back specific batches)
- Financial-grade correctness

### Earning rules (configurable per business)

A business can configure earning rules in JSON:

```json
{
  "feedback_submitted": { "points": 10, "max_per_day": 1 },
  "feedback_with_comment": { "points": 25, "min_chars": 30, "max_per_day": 1 },
  "issue_reported": { "points": 50, "max_per_week": 3 },
  "issue_resolved_confirmation": { "points": 30, "max_per_issue": 1 },
  "public_review_left": { "points": 100, "max_per_business_per_year": 4, "verification": "manual" }
}
```

The engine reads these rules and decides the points awarded. Rules are evaluated server-side only — never trust the client.

### Anti-fraud (designed in, not bolted on)

Every points-earning event runs through a **fraud scorer** before the ledger entry is written. Score 0 = clean, score 100 = blocked.

Signals:

| Signal | Weight | Detail |
|---|---|---|
| Velocity | 30 | More than N feedback in M minutes from same fingerprint/IP |
| Fingerprint duplication | 25 | Same device fingerprint, multiple "different" sessions |
| IP clustering | 20 | Many distinct sessions from same IP in short window |
| Geo anomaly | 15 | Session location ≠ guest geo-IP (within reason — VPNs exist) |
| Text similarity | 10 | Comment is near-duplicate of recent comments (Levenshtein ratio > 0.9) |
| Account-link velocity | 30 | Email being linked to many sessions across many businesses fast |
| Reward redemption velocity | 40 | Multiple redemption attempts < 1min |
| Honeypot trip | 100 | Hidden form field was filled (bot signal) |

Score thresholds:
- **0–30**: Award points normally
- **31–70**: Award points but flag for review (`points_ledger.flagged = true`)
- **71–100**: Award **pending** points (not redeemable for 24h, reviewable by staff)
- **>100** (honeypot): Block submission entirely, log security event

### Redemption flow

```
User browses business reward catalog
  │
  ▼
User taps "Redeem" on a reward
  │
  ▼ (POST /api/rewards/{id}/redeem)
Server:
  1. Lock user_account row (SELECT FOR UPDATE)
  2. Verify SUM(points_ledger.amount) >= reward.cost
  3. Check redemption velocity (anti-fraud)
  4. Check reward.stock > 0
  5. Insert ledger entry: -reward.cost (reason: redemption)
  6. Insert redemption row with status='pending', code=generated
  7. Decrement reward.stock atomically
  8. Commit transaction
  │
  ▼
Server pushes redemption_code to user (PWA + email)
  │
  ▼
User shows code at business
  │
  ▼
Staff scans/enters code in dashboard → marks redemption.status='claimed'
```

All of steps 1–7 are in a single Postgres transaction with `SERIALIZABLE` isolation. No race conditions, no negative balances.

### Reward catalog

Per-business catalog. Each reward has:
- `name`, `description`, `image_url`
- `cost` (points)
- `stock` (or null for unlimited)
- `expiry_days` (after redemption)
- `terms` (free text)
- `active` (bool)

---

## 7. Background jobs (Hangfire)

| Job | Trigger | Purpose |
|---|---|---|
| `ClassifyFeedbackJob` | On feedback insert | Run AI classification, update row, broadcast |
| `ExpirePointsJob` | Daily at 02:00 | Mark old ledger entries expired (per business config) |
| `RecomputeFraudScoresJob` | Hourly | Re-score recent activity with updated patterns |
| `SendDigestEmailJob` | Daily at 08:00 (per business TZ) | Email summary to business owners |
| `IssueAutoEscalateJob` | Every 5 min | Escalate issues open > SLA |
| `ReviewLinkExpireJob` | Every 1 min | Expire unused review redirect tokens |
| `MetricsRollupJob` | Every 5 min | Pre-aggregate dashboard counters |

Hangfire dashboard mounted at `/hangfire` (admin only, IP-restricted in production).

---

## 8. Data flow: end-to-end feedback example

User scans QR for Room 204 at Oceanview Hotel:

```
1. Browser → GET /r/abc123xyz
   API:
     - Look up short_code → location_id, business_id
     - Create feedback_session row (anonymous)
     - Set HMAC-signed cookie: sess=<token>
     - Return SPA HTML with embedded session config

2. Guest taps "Could be better" + types "AC isn't working"
   Browser → POST /api/feedback
   Headers: Cookie: sess=<token>
   Body: { rating: 2, comment: "AC isn't working", category_hint: "Room" }

3. API:
   - Validate session token, extract session_id, business_id, location_id
   - SET LOCAL app.current_business_id = ...
   - Insert feedback row (classification: pending)
   - Run synchronous fraud scorer (cheap, local)
   - If score < 71: insert points_ledger entry (+10 for feedback_submitted)
   - Enqueue ClassifyFeedbackJob
   - Broadcast: feedback.created → business:{id} group
   - Return 201 { feedback_id, points_earned: 10, status: "received" }

4. Browser receives response, shows "We're on it" screen.
   SignalR connection joins group session:{sessionId}.

5. Hangfire worker picks up ClassifyFeedbackJob:
   - Call AI classifier → { sentiment: "negative", category: "maintenance",
     severity: "medium", route_to: "Maintenance" }
   - Update feedback row
   - Create issue row, assigned to Maintenance department
   - Broadcast: issue.created → business:{id} + business:{id}:dept:{maintId}

6. Maintenance staff dashboard receives issue.created event,
   issue appears in Live Feed without page reload.

7. Staff member taps "Start" → POST /api/issues/{id}/status { status: "in_progress" }
   - Update issue row
   - Broadcast: issue.status_changed → both business group AND session:{sessionId}

8. Guest's PWA receives issue.status_changed → animates timeline marker.

9. Staff resolves → POST /api/issues/{id}/status { status: "resolved", note: "..." }
   - Update issue row
   - Insert points_ledger entry (+30 for issue_resolved_confirmation pending guest confirm)
   - Broadcast: issue.resolved → session:{sessionId}

10. Guest sees "Your issue has been resolved 🎉" → taps "Yes, all good"
    → POST /api/issues/{id}/confirm-resolution
    - Confirm points_ledger entry (set status=confirmed)
    - Broadcast: points.earned (+30) → session:{sessionId}
    - If positive: prompt "Save your points? Enter email."
```

This entire flow is < 100ms server-side per request, with the AI step happening async in 1–3s.

---

## 9. Security posture

### At the edge (Azure)
- WAF (Azure Front Door or Application Gateway with WAF tier)
- DDoS standard (free with Azure)
- TLS 1.3, HSTS, certificate auto-rotation via Key Vault

### At the API
- Rate limiting per IP + per session + per user (different tiers)
- Aspire-style structured logging with correlation IDs
- All secrets in Azure Key Vault, never in env files in production
- JWT signing keys rotate every 90 days (graceful with key versioning)
- CORS locked to known origins

### At the database
- Postgres flexible server with private endpoint (no public IP in prod)
- RLS enforced on all tenant tables
- Backup: PITR enabled, 30-day retention
- Connection string from Key Vault, rotated quarterly
- Read replica for analytics queries (don't burn primary on dashboards)

### At the application
- All input validated with FluentValidation
- All output escaped (anti-XSS — React handles this for views, server is strict for emails/SMS)
- File uploads: virus scan via Azure Defender, MIME sniffing, size cap, separate domain for serving
- CSRF: double-submit cookie pattern for state-changing endpoints
- Honeypot fields on all guest forms

### GDPR/privacy
- Right to erasure: `DELETE /api/me` triggers anonymization job (PII fields nulled, ledger entries marked anonymized but retained for accounting integrity)
- Right to access: `GET /api/me/export` produces JSON dump
- Cookie consent banner (guest PWA), only essentials before consent
- Data residency: Azure region pinned to EU for EU customers (your Albania user base is fine in West Europe)

---

## 10. What we're explicitly NOT building in v1

This is as important as what we are building. **Discipline here saves you 4 weeks.**

- ❌ Native iOS/Android apps (PWA only — covers 99% of cases)
- ❌ SMS/WhatsApp inbound feedback (URL-only)
- ❌ White-label custom domains per business (subdomain scheme: `{biz-slug}.onpoint.ai/r/{code}`)
- ❌ Cross-business reward marketplace (per-business catalogs only)
- ❌ Advanced analytics dashboards (basic counters + trend lines only)
- ❌ Public API for third-party integrations
- ❌ Stripe billing (manual invoicing for first 10 customers — focus on product)
- ❌ Multi-language UI (English only at launch, i18n hooks in code from day one)
- ❌ Voice feedback / video feedback
- ❌ Sentiment over time charts (just "today vs last 7 days" tile)

If a customer asks for any of these in pre-sales, write them down for v2. Don't promise v1.

---

## 11. Strategic UX revisions to your Figma flows

Your Figma is mostly excellent. Three changes I'd recommend:

### 1. Remove the "Quick Feedback" three-button screen for the first interaction

**Your design:** Good / Okay / Not good buttons.
**Problem:** Three options is a Likert-3, which loses the granularity of 1–5 stars. Your spec says 1–5 rating but Figma shows 3 emoji buttons.
**Recommendation:** Use a 5-point scale with emoji faces (😞 😕 😐 🙂 😍). Same speed, more granular data, better routing.

### 2. The "How is your experience so far?" screen should default to no rating selected

In the Figma, the buttons look pre-styled by sentiment. That's fine, but make sure the user has to actively tap one — don't auto-select. Auto-selection biases responses upward.

### 3. The points/rewards UI is missing from your Figma

Add it. Suggested placement:
- After issue resolution, on the "Thanks" screen: "+30 points earned" with optional "save my points" CTA.
- Persistent points badge in header (only after account creation).
- New screen: **Rewards catalog** for the current business, accessible from the post-resolution screen and from a hamburger menu after account creation.

---

## 12. The minimum viable cloud (Azure footprint)

For MVP launch:

| Resource | Tier | Monthly cost (est.) |
|---|---|---|
| Azure App Service (API) | P1v3 (1 instance) | ~$80 |
| Azure SignalR Service | Standard, 1 unit | ~$50 |
| Azure Database for PostgreSQL Flexible | B2s | ~$80 |
| Azure Cache for Redis | Basic C1 | ~$25 |
| Azure Static Web Apps (3x: guest, staff, admin) | Standard | ~$30 |
| Azure Key Vault | Standard | ~$5 |
| Azure Storage (blobs) | Standard LRS | ~$10 |
| Azure Application Insights | Pay-as-you-go | ~$20 |
| Azure Front Door | Standard | ~$35 |
| Azure Communication Services (email/SMS) | Pay-per-use | ~$20 |
| **Total** | | **~$355/month** |

This handles ~50,000 feedback submissions/month and 50 concurrent businesses comfortably. Scale path: bump App Service to P2v3, scale SignalR to 2 units, add a Postgres read replica. That gets you to 500 businesses without architectural changes.
