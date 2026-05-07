# CLAUDE.md — OnPoint AI Engineering Manual

> **READ THIS ENTIRE FILE BEFORE WRITING ANY CODE.**
> This is the source of truth for how to build, extend, test, and deploy
> this project. Every code change must respect the rules in this document.
> If something in this file conflicts with what the user asks, ask for
> clarification — do not silently deviate.

---

## TABLE OF CONTENTS

1. [Project Overview](#project-overview)
2. [Mandatory Pre-Code Workflow](#mandatory-pre-code-workflow)
3. [Architecture & Tech Stack](#architecture--tech-stack)
4. [Repository Layout](#repository-layout)
5. [The AI Pipeline](#the-ai-pipeline)
6. [AI Engineering Constraints](#ai-engineering-constraints)
7. [Three User Layers](#three-user-layers)
8. [Database & Multi-Tenancy](#database--multi-tenancy)
9. [API Contract](#api-contract)
10. [TypeScript Type Discipline](#typescript-type-discipline)
11. [Frontend Design System](#frontend-design-system)
12. [Real-Time Updates (WebSocket)](#real-time-updates-websocket)
13. [Coding Rules — Universal](#coding-rules--universal)
14. [Testing Strategy](#testing-strategy)
15. [Security Requirements](#security-requirements)
16. [Performance & Scalability](#performance--scalability)
17. [Deployment Pipeline](#deployment-pipeline)
18. [Phase Roadmap & Current State](#phase-roadmap--current-state)
19. [Workflow for Every Task](#workflow-for-every-task)
20. [Forbidden Actions](#forbidden-actions)

---

## PROJECT OVERVIEW

**OnPoint AI** is an AI-first hospitality management platform for hotels,
restaurants, and any customer-experience business. Guests scan a QR code
in their hotel room and report issues via voice or text on their phone.
The AI pipeline categorizes, prioritizes, and routes the issue to the
right department and staff member in under 100 milliseconds. Staff see
a real-time dashboard with AI recommendations. Admins get predictive
analytics. The system learns from every resolved issue and gets smarter
over time.

**Three pillars:**

1. **Frictionless guest experience** — no login, no app download, no signup
2. **AI-first decision making** — every action has model-driven intelligence behind it
3. **Continuous learning** — every resolved issue feeds back into training

**Initial demo target:** hotels in Albania, presented at Albanian innovation events.
**Long-term scope:** any hospitality or customer-facing business globally.

---

## MANDATORY PRE-CODE WORKFLOW

**Every task begins with the same three steps. Skip none of them.**

### Step 0 — Full Project Scan

Before writing a single line of code, perform a complete scan of the
files relevant to the task. Read each one fully — not just the
declarations.

After scanning, produce a written summary covering:

- **MISMATCHES** — anything in the user's request that conflicts with the
  current codebase (wrong field names, wrong DbSet names, wrong API
  paths, wrong enum values, outdated assumptions about phase progress)
- **ALREADY EXISTS** — any file, class, method, route, or registration
  the request would create that already exists
- **IMPROVEMENTS** — any pattern in the existing code worth following
  more closely, or any small refactor that would make the new work fit
  better

**Do not write code until this summary is produced and acknowledged.**

### Step 1 — State the Phase

After the scan, state which phase of the roadmap this task belongs to
(see [Phase Roadmap & Current State](#phase-roadmap--current-state)).
If the task spans multiple phases, propose splitting it.

### Step 2 — Confirm the Plan

State explicitly:

- Files that will be created
- Files that will be modified
- Files explicitly **not** touched
- Any new dependencies and why they are needed
- The verification steps the user will run after code is written

Wait for user acknowledgment before proceeding.

---

## ARCHITECTURE & TECH STACK

### Backend

- **.NET 10**, ASP.NET Core, EF Core 10
- **PostgreSQL 16** with Row-Level Security (RLS) — every tenant request
  sets `app.current_business_id` via `TenantResolutionMiddleware`
- **Auth:** JWT for staff (in memory only — never localStorage), session
  cookie (`op_session`) for guests
- **Real-time:** SignalR hub for WebSocket updates (issue status, dashboard stats)
- **Background workers:** Hangfire or .NET BackgroundService for async AI
  inference, model retraining triggers, analytics rollups
- **Logging:** Serilog with structured logs, written to console in dev
  and to Azure Application Insights in production
- **Runs on:** `http://localhost:5000` in dev

### AI Layer

- **Inference service:** Python FastAPI microservice (`ai-service/`)
  exposing gRPC or HTTP endpoints. Models are served behind a thin API
  so the .NET backend stays language-agnostic.
- **Models:**
  - Sentiment + classification: fine-tuned distilled BERT or a hosted
    LLM (GPT-4o-mini, Claude Haiku) for early demos
  - Priority ranker: gradient-boosted model on engineered features
  - Department router: rule-based + ML hybrid
  - Solution recommender: retrieval over historical resolved issues
  - Satisfaction predictor: regression on rating + sentiment + resolution time
  - Chatbot: hosted LLM with retrieval over hotel-specific knowledge base
- **Storage:** all model predictions, scores, and explanations stored in
  Postgres (`ai_predictions` table) for retraining and audit
- **Voice:** OpenAI Whisper (or Azure Speech) for transcription
- **Inference latency budget:** < 100ms per inference call. Anything
  slower goes to a background queue, not the request path.

### Frontend

- **Vite + React + TypeScript + Tailwind CSS** (SPA)
- **React Router v6** for routing
- **Framer Motion** for animations
- **Geist font** (Vercel)
- **SignalR client** for WebSocket live updates
- **Runs on:** `http://localhost:5173` in dev
- **Vite proxy** forwards `/api`, `/r`, `/health`, `/hubs` to backend

### Infrastructure

- **Database:** PostgreSQL on `localhost:5433` (Docker container `onpoint-db`)
- **Cache:** Redis (planned) for session cache and AI inference cache
- **Cloud target:** Azure App Service (backend), Azure Static Web Apps (frontend),
  Azure Database for PostgreSQL Flexible Server, Azure Container Apps for AI service
- **CI/CD:** GitHub Actions (build, test, deploy on merge to main)

---

## REPOSITORY LAYOUT

```
onpoint-ai/
├── backend/
│   ├── src/
│   │   ├── OnPoint.Domain/              entities only, no logic
│   │   ├── OnPoint.Application/         interfaces only (ITenantContext, IAiService, etc.)
│   │   ├── OnPoint.Infrastructure/      EF Core, handlers, JWT, SignalR hubs, AI client
│   │   └── OnPoint.API/                 controllers, middleware, Program.cs
│   ├── tests/
│   │   ├── OnPoint.UnitTests/           handler tests with in-memory DB
│   │   ├── OnPoint.IntegrationTests/    full HTTP tests against Testcontainers Postgres
│   │   └── OnPoint.AiContractTests/     contract tests against AI service mocks
│   └── OnPoint.sln
├── ai-service/
│   ├── app/
│   │   ├── main.py                      FastAPI entrypoint
│   │   ├── pipeline/                    each AI stage as a separate module
│   │   │   ├── sentiment.py
│   │   │   ├── classifier.py
│   │   │   ├── priority.py
│   │   │   ├── router.py
│   │   │   ├── matcher.py
│   │   │   ├── recommender.py
│   │   │   ├── satisfaction.py
│   │   │   └── chatbot.py
│   │   ├── models/                      model loading and caching
│   │   └── learning/                    retraining triggers and feedback ingestion
│   ├── tests/
│   ├── pyproject.toml
│   └── Dockerfile
├── frontend/
│   ├── src/
│   │   ├── api/                         typed HTTP client + per-resource modules
│   │   ├── components/
│   │   ├── components/motion/           reusable animation primitives
│   │   ├── contexts/
│   │   ├── hooks/
│   │   ├── pages/
│   │   ├── realtime/                    SignalR client setup
│   │   ├── types/
│   │   └── utils/
│   ├── tests/                           Vitest + React Testing Library
│   └── package.json
├── database/
│   └── migrations/                      raw SQL migration files (numbered)
├── docs/
│   ├── architecture/
│   ├── ai/                              model cards, prompts, eval reports
│   └── runbooks/                        ops procedures
├── infra/
│   ├── azure/                           Bicep / Terraform IaC
│   └── github-actions/                  workflow files
├── docker-compose.yml                   dev stack (postgres, redis, ai-service)
├── CLAUDE.md                            this file
└── README.md
```

---

## THE AI PIPELINE

Every guest issue flows through a deterministic pipeline. Each stage
produces a prediction record stored in the database.

```
Guest input (voice or text)
        │
        ▼
┌─────────────────────────────────────────┐
│ 1. Voice Transcription (if voice input) │
│    Output: text                         │
└─────────────────────────────────────────┘
        │
        ▼
┌─────────────────────────────────────────┐
│ 2. Sentiment Analyzer                   │
│    Output: { sentiment, urgency_score } │
└─────────────────────────────────────────┘
        │
        ▼
┌─────────────────────────────────────────┐
│ 3. Issue Classifier                     │
│    Output: { category, confidence }     │
└─────────────────────────────────────────┘
        │
        ▼
┌─────────────────────────────────────────┐
│ 4. Priority Ranker                      │
│    Output: { priority_score 0-100 }     │
└─────────────────────────────────────────┘
        │
        ▼
┌─────────────────────────────────────────┐
│ 5. Department Router                    │
│    Output: { department_id, alts[] }    │
└─────────────────────────────────────────┘
        │
        ▼
┌─────────────────────────────────────────┐
│ 6. Staff Matcher (background or sync)   │
│    Output: { staff_user_id, alts[] }    │
└─────────────────────────────────────────┘
        │
        ▼
┌─────────────────────────────────────────┐
│ 7. Solution Recommender                 │
│    Output: { top_5_solutions[] }        │
│    Each: { text, success_rate, source } │
└─────────────────────────────────────────┘
        │
        ▼
┌─────────────────────────────────────────┐
│ 8. Satisfaction Predictor               │
│    Output: { predicted_satisfaction }   │
└─────────────────────────────────────────┘
        │
        ▼
   Issue created, guest sees status,
   staff sees ranked queue + AI recs
```

**Chatbot (stage 9)** runs in parallel — the guest can interact with the AI
assistant while waiting. The chatbot has access to the issue context and
the recommended solutions.

**Continuous Learning (stage 10)** triggers on every resolved issue:
- Capture: predicted vs actual category, predicted vs actual department,
  predicted vs actual satisfaction, time to resolve, which solution was used
- Aggregate: nightly job rolls up performance metrics per model
- Retrain: weekly job retrains models when training set grows past threshold
- Deploy: new model versions deploy behind a feature flag, with canary rollout

---

## AI ENGINEERING CONSTRAINTS

These constraints are non-negotiable. Every piece of AI work must respect them.

1. **Inference latency budget: < 100ms per stage.** If a stage cannot meet
   this, it runs in a background queue and the API responds optimistically.
   The pipeline never blocks the user-facing request beyond 500ms total.

2. **Every prediction is stored.** Schema: `ai_predictions` table with
   columns `id`, `issue_id`, `stage`, `input_hash`, `output_json`,
   `model_version`, `confidence`, `latency_ms`, `created_at`.
   This is the training corpus and the audit log.

3. **Staff can override every AI decision.** The dashboard shows AI
   reasoning (top features, confidence scores) and provides one-click
   overrides. Overrides are logged and feed back into training as
   negative signal.

4. **No AI decision is hidden.** "Why did this go to Maintenance?" must
   always be answerable. Every prediction record includes a human-readable
   explanation field.

5. **Graceful degradation.** If the AI service is unreachable, the
   backend falls back to deterministic rules (regex categorization,
   round-robin routing) and tags the issue `ai_fallback: true`. The app
   keeps working.

6. **Confidence thresholds gate auto-routing.** If classifier confidence
   < 0.7, the issue lands in a "Needs Review" queue for staff to
   triage. Don't auto-route low-confidence predictions.

7. **No training on PII.** Guest names, emails, phone numbers are
   stripped or hashed before any prediction is added to the training
   corpus. The `feedback` and `issues` tables flag PII fields explicitly.

8. **Model versions are immutable.** When a model is retrained,
   it gets a new version string (semver). Old predictions retain their
   original model version for auditability.

9. **Prompts are versioned and stored in source control.** Any prompt
   sent to a hosted LLM lives in `ai-service/app/pipeline/prompts/` and
   is reviewed like code.

10. **Cost ceiling per request.** The full pipeline must cost less than
    USD 0.01 per issue processed at expected volume. Track cost per
    inference. Use distilled or local models where possible; reserve
    hosted LLMs for the chatbot and complex reasoning.

---

## THREE USER LAYERS

### Guest (mobile, no login)

- Enters via `GET /r/{shortCode}` — backend sets `op_session` cookie,
  redirects to `/feedback`
- Submits via `POST /api/feedback` (cookie auth)
- Polls `GET /api/issues/{id}` or subscribes via SignalR for live status
- Chats with AI via `POST /api/chat/{sessionId}`
- Earns points stored in `points_ledger` (immutable, INSERT only)
- Never sees the staff dashboard or admin panel

### Staff (desktop, JWT auth)

- Logs in via `POST /api/auth/staff/login`
- Sees real-time issue queue ranked by AI priority score
- Each issue card shows: AI category, priority, recommended department,
  recommended staff member, top 3 solutions with success rates,
  predicted satisfaction
- Can override any AI decision in one click
- Marks issues as Started, Assigned, Resolved
- Sees AI reasoning panel ("Why this priority?")

### Admin (desktop, JWT auth, role = admin)

- Everything staff can do, plus:
- Manages locations (rooms), departments, staff users
- Views analytics dashboard:
  - Issue volume trends, resolution time trends
  - Staff performance leaderboard
  - AI accuracy metrics (predicted vs actual satisfaction)
  - Recurring problem detection ("Room 204 has 5 AC issues this month")
  - Predictive insights ("Housekeeping response slows 40% on weekends")
- Configures AI behavior:
  - Confidence thresholds for auto-routing
  - SLA targets per department
  - Custom solution recommendations
- Reviews AI overrides and feedback for training quality

---

## DATABASE & MULTI-TENANCY

### Schema discipline

Multi-tenant via shared schema + Row-Level Security. Every tenant-scoped
table has a `business_id` column with an RLS policy:

```sql
CREATE POLICY tenant_isolation ON {table_name}
  USING (business_id = current_setting('app.current_business_id')::uuid);
```

Every request middleware sets `app.current_business_id` from the JWT
or the `op_session` cookie before any query runs.

### Key tables

- `businesses` — tenant root. Soft-deleted via `deleted_at`.
- `staff_users`, `business_memberships` — staff identity
- `guest_users` — optional, only when guest opts into rewards
- `locations` — rooms/tables with unique short codes
- `departments` — handles category-to-department routing
- `feedback_sessions`, `feedback`, `issues` — the core flow
- `ai_predictions` — every AI decision logged here
- `points_ledger` — immutable rewards ledger (INSERT only, never UPDATE)
- `model_versions` — tracks which model produced which prediction

### Migration rules

- All schema changes go through numbered SQL migrations in `database/migrations/`
- Never drop columns in the same migration where you stop reading from them.
  Use a two-deploy pattern: stop reading → deploy → drop in next migration.
- Every new table needs an RLS policy in the same migration.
- Every migration is reviewed for index coverage on foreign keys and
  on columns used in WHERE clauses.

### Soft delete pattern

- `Business`, `StaffUser`, `GuestUser`, `Location` use `DeletedAt`
- `Department` uses `IsActive` flag (not soft delete)
- All queries on these entities must filter `DeletedAt IS NULL` unless
  the caller explicitly opts in via `IncludeInactive=true`

---

## API CONTRACT

### Public (no auth)

```
GET  /health                          → { status: "Healthy" }
GET  /r/{shortCode}                   → 200 + { sessionId } + sets op_session
```

### Staff auth (no token required)

```
POST /api/auth/staff/register
POST /api/auth/staff/login
```

### Guest (op_session cookie)

```
POST /api/feedback                    → triggers AI pipeline
POST /api/chat/{sessionId}            → AI chatbot
GET  /api/issues/{id}                 → status polling
```

### Staff JWT (Authorization: Bearer)

```
GET    /api/dashboard/stats
GET    /api/issues?status=&departmentId=&locationId=&priority=&page=&pageSize=
GET    /api/issues/{id}
GET    /api/issues/{id}/ai-explanation       → why this category, priority, routing
POST   /api/issues/{id}/start
POST   /api/issues/{id}/resolve              → triggers learning loop
PATCH  /api/issues/{id}/assign
PATCH  /api/issues/{id}/override             → log AI override

GET    /api/locations
POST   /api/locations
PUT    /api/locations/{id}
DELETE /api/locations/{id}
GET    /api/locations/{id}/qr

GET    /api/departments
POST   /api/departments
PUT    /api/departments/{id}
DELETE /api/departments/{id}

GET    /api/analytics/overview               → admin only
GET    /api/analytics/ai-accuracy            → admin only
GET    /api/analytics/recurring-problems     → admin only
```

### Real-time (SignalR)

```
/hubs/issues       → live issue updates per business
/hubs/dashboard    → live dashboard stats per business
/hubs/guest/{id}   → live status for a single guest session
```

### Error format

```json
{ "error": "Human-readable message", "code": "MACHINE_READABLE_CODE" }
```

- `400` validation
- `401` unauthenticated
- `403` authenticated but not allowed (wrong tenant, wrong role)
- `404` not found
- `409` state conflict (e.g. delete department with active issues)
- `422` AI service unavailable, fallback in use
- `429` rate limited
- `5xx` server error (logged with correlation ID)

### Domain enums (exact string values)

```
IssueStatus:       open, assigned, in_progress, resolved, cancelled
IssuePriority:     low, medium, high, urgent
LocationType:      room, table, public_area, department, service_point, other
PointsEntryStatus: confirmed, pending_review, reversed, expired
SentimentLabel:    positive, neutral, negative, urgent
```

---

## TYPESCRIPT TYPE DISCIPLINE

C# `PascalCase` properties serialize to `camelCase` in JSON. Every
TypeScript interface in `frontend/src/types/index.ts` must match the
backend DTO field-by-field. **Mismatches cause silent `undefined`
errors that take hours to debug.**

### Verification rule

Every time a new type is added or an existing type is changed:

1. Open the C# DTO record in the backend
2. Confirm every property name maps correctly (PascalCase → camelCase)
3. Confirm every property type maps correctly (`Guid?` → `string | null`,
   `DateTime` → `string`, `string?` → `string | null`)
4. Add a comment above the TypeScript interface naming the C# source record

### Known correct mappings

```typescript
// IssueListItem (from GET /api/issues)
interface IssueSummary {
  issueId: string         // NOT id
  title: string
  description: string | null
  status: IssueStatus
  priority: IssuePriority
  locationName: string | null
  departmentId: string | null
  departmentName: string | null
  createdAt: string
  resolvedAt: string | null
}

// IssueDetailResponse (from GET /api/issues/{id})
interface IssueDetail {
  issueId: string
  title: string
  description: string | null
  priority: IssuePriority    // NOT IssueStatus
  status: IssueStatus        // NOT IssuePriority
  locationId: string | null
  locationName: string | null
  departmentId: string | null
  departmentName: string | null
  assignedTo: string | null
  resolvedBy: string | null
  feedbackId: string
  feedbackRating: number     // NOT rating
  feedbackComment: string | null   // NOT comment
  createdAt: string
  updatedAt: string
  resolvedAt: string | null
  // AI fields
  aiCategory: string | null
  aiCategoryConfidence: number | null
  aiPriorityScore: number | null
  aiRecommendedSolutions: AiSolution[] | null
  aiPredictedSatisfaction: number | null
}
```

---

## FRONTEND DESIGN SYSTEM

The visual identity is **Wise.com colors + Partiful.com dynamics + Apple
iOS native feel**. Premium, alive, trustworthy. Never generic SaaS.

### Color palette

```
Guest dark backgrounds:
  Ink 950: #071A0A   (page bg — dark green-black, NOT pure #000000)
  Ink 800: #163B1E   (card surface on dark)
  Ink 700: #1E4D28   (elevated surface)

Brand green (Wise signature):
  Primary: #9FE870   (CTAs, active states, voice button, success)
  Dark:    #4A9922   (text on light, gradient end)
  Light:   #F0FDE4   (tint backgrounds on staff side)

Staff light backgrounds:
  Page:  #F9FAF7    (warm white with green undertone)
  Card:  #FFFFFF
  Hover: #F5F8F0

Status (semantic):
  Resolved/Positive: #9FE870
  In Progress:       #9FE870 with pulse animation
  Urgent/Error:      #E8372C
  Medium/Caution:    #FFB020
  Low/Inactive:      #8E8E93
```

### Typography

- Font: **Geist** (already installed)
- Guest headlines: 32–34px / Bold / -0.03em tracking
- Staff page titles: 28px / Bold / -0.02em tracking
- Body: 15px / Regular / 1.6 line-height
- Labels: 11px / uppercase / 0.06em tracking

### Buttons

- Staff primary: `#1A1A1A` fill, white text, 10px radius (Uber-style)
- Guest primary: `#9FE870` fill, `#071A0A` text (Wise-style green CTA)
- Ghost: transparent, 1px `rgba(0,0,0,0.12)` border
- Destructive: `#E8372C` fill, white text

### Animation principles

- Page enter: fade + slide up 8px, 300ms, spring
- Card stagger: 50ms between items
- Button press: scale 0.97, spring back
- Number count-up: animate 0 → value, 800ms ease-out
- Voice rings: scale 1 → 1.5, opacity 0.12 → 0, 2s loop
- Live dot pulse: opacity 1 → 0.4 → 1, 2s loop
- Skeleton: shimmer gradient sweep, 1.5s loop
- Respect `prefers-reduced-motion` everywhere

### Format rules

- Never hardcode `http://localhost:5000` in frontend source — use Vite proxy
- All styling via Tailwind unless a brand color is not in the config
- No inline styles for spacing or colors that have utility classes

---

## REAL-TIME UPDATES (WEBSOCKET)

### Server: SignalR hubs

```csharp
public class IssuesHub : Hub
{
    // Connect with JWT, joined to group "biz:{businessId}"
    // Broadcast events:
    //   IssueCreated, IssueUpdated, IssueAssigned, IssueResolved
}

public class GuestStatusHub : Hub
{
    // Connect with op_session cookie, joined to group "session:{sessionId}"
    // Broadcast events:
    //   StatusChanged, AiSuggestionAdded, ChatMessage
}
```

### Client

- Frontend reconnects automatically on disconnect with exponential backoff
- All real-time events also persist via REST endpoints — WebSocket is an
  enhancement, not the only path. The app must work even if WebSocket fails
  (fall back to 10-second polling)

### Event flow on issue resolution

```
Staff clicks Resolve
  → POST /api/issues/{id}/resolve
  → DB updated
  → IssuesHub.SendAsync("IssueResolved", issueId)
  → GuestStatusHub.SendAsync("StatusChanged", { issueId, status: "resolved" })
  → Both staff dashboard and guest screen update within 200ms
  → Background worker: enqueue training feedback, recompute analytics
```

---

## CODING RULES — UNIVERSAL

1. **Read before writing.** Always perform the Step 0 scan.
2. **No placeholder comments.** Never write `// TODO`, `// implement later`,
   or stub functions that throw `NotImplementedException`. Every file
   must be fully functional.
3. **Layering is one direction.** `Domain ← Application ← Infrastructure ← API`.
   Never reverse this. The Domain depends on nothing. The API depends on everything.
4. **Handlers live in `OnPoint.Infrastructure`.** Not in Application —
   handlers depend on `AppDbContext` and `JwtIssuer`, which are
   infrastructure concerns.
5. **`businessId` always comes from `ITenantContext`** in controllers,
   passed as a parameter to handlers. Never extract from the JWT inside
   the handler.
6. **Points ledger is immutable.** Only INSERT. Never UPDATE amounts.
   Reversals are new INSERT rows with negative amounts and a `reversed_by` link.
7. **JWT is in memory only on the frontend.** Use React Context. Never
   localStorage or sessionStorage. Token expires in 60 minutes; the
   frontend handles refresh or re-login.
8. **The `website` field on the feedback form is a honeypot.** Hidden
   via CSS off-screen positioning (`position: absolute; left: -9999px`),
   never `display: none`. Never label it. Real users send empty string.
9. **All timestamps are UTC** in the database and over the wire. Frontend
   converts to local for display only.
10. **Migrations are forward-only.** Never edit a migration after it has
    been applied to a shared environment. Write a new one.
11. **Every new endpoint has a happy-path integration test** before it
    merges to main.
12. **Every AI prediction is stored** before the response returns.
    No exceptions.
13. **Logs are structured.** No `Console.WriteLine` in production code.
    Use `ILogger<T>` with structured fields.
14. **Secrets never in source.** Use environment variables in dev,
    Azure Key Vault in production. `appsettings.json` may contain only
    non-sensitive config.

---

## TESTING STRATEGY

### Backend

- **Unit tests** (`OnPoint.UnitTests`) — fast tests on handlers using
  in-memory or SQLite EF Core. Cover business logic, validation, and
  state transitions. Goal: every handler has at least 3 tests
  (happy path, error path, edge case).

- **Integration tests** (`OnPoint.IntegrationTests`) — full HTTP tests
  against a real Postgres via Testcontainers. Cover end-to-end flows:
  register → login → submit feedback → assign → resolve. Goal: every
  API route has an integration test.

- **AI contract tests** (`OnPoint.AiContractTests`) — mock the AI service
  with deterministic responses. Verify the backend handles every output
  shape (high confidence, low confidence, fallback, error).

- **Coverage targets:** 80% line coverage minimum on Domain and
  Infrastructure. 100% on critical paths (auth, RLS, points ledger).

### AI service

- **Unit tests** on each pipeline stage with fixed inputs and golden outputs
- **Eval harness** — a labeled dataset of historical issues with ground-truth
  category, priority, and resolution. Run on every model version. Track
  accuracy, F1, latency. Block deployment if any metric regresses by > 2%.
- **Load tests** — k6 or Locust hitting the AI service at expected peak
  volume. Verify p99 latency stays under 100ms per stage.

### Frontend

- **Component tests** (Vitest + React Testing Library) — test rendering,
  user interactions, and state transitions. Goal: every interactive
  component has tests.
- **E2E tests** (Playwright) — critical user flows: guest scan → submit →
  see status, staff login → resolve issue. Run in CI on every PR.

### CI gating

A PR cannot merge to main unless:
- All unit tests pass
- All integration tests pass
- E2E tests pass
- Linter passes (ESLint for TS, Roslyn analyzers for C#, ruff for Python)
- Code coverage does not decrease
- AI eval suite does not regress

---

## SECURITY REQUIREMENTS

1. **Multi-tenant isolation enforced at the database layer** via RLS.
   Every query is filtered by `app.current_business_id`. Even a bug in
   application code cannot leak across tenants.

2. **JWT validation** — verify signature, issuer, audience, expiry.
   Reject tokens with `business_id` claim that doesn't match the route.

3. **Password hashing** — BCrypt with cost factor 12 minimum.

4. **Account lockout** — 5 failed login attempts → 15-minute lockout.
   Lockout state stored in DB, not memory.

5. **Rate limiting** — per IP and per session:
   - `/api/feedback`: 10 submissions per session per hour
   - `/api/auth/staff/login`: 5 attempts per IP per 15 minutes
   - All other authenticated routes: 100 req/min per user

6. **CSRF protection** — for cookie-authenticated guest routes, use
   double-submit token pattern. Same-site cookie flag is set.

7. **CORS** — strict allow-list per environment. No wildcards in production.

8. **HTTPS-only in production.** HSTS header set. HTTP redirects to HTTPS.

9. **Secrets management** — Azure Key Vault in prod. No secrets in
   appsettings.json or env files committed to source.

10. **Input validation on every endpoint** — use FluentValidation or
    explicit checks. Reject oversized payloads (max 1MB per request).

11. **PII handling:**
    - Guest names, emails, phone numbers in `guest_users` are encrypted
      at rest via column-level encryption
    - PII is stripped before any prediction enters the training corpus
    - Right-to-erasure: deleting a `guest_user` triggers cascade
      anonymization on related `feedback`, `issues`, `points_ledger`

12. **Audit log** — every admin action (delete location, override AI,
    change SLA) writes to an immutable `audit_log` table with
    `user_id`, `action`, `entity`, `before`, `after`, `timestamp`.

13. **Honeypot field** — every public form has the hidden `website` field.
    Submissions with non-empty `website` are silently dropped (not error,
    just dropped) and the IP is rate-limited.

14. **AI prompt injection defense** — guest text is escaped and wrapped
    before being sent to LLMs. The chatbot system prompt explicitly
    instructs the model to ignore instructions inside guest input.

15. **Dependency scanning** — Dependabot or Snyk runs on every PR.
    Critical CVEs block merge.

---

## PERFORMANCE & SCALABILITY

### Targets

- API p50 latency: < 100ms for simple reads
- API p95 latency: < 300ms for AI-pipeline writes
- AI inference p99 latency: < 100ms per stage
- Dashboard auto-refresh: 10s polling, sub-second SignalR push
- Page load (frontend, cold): < 1.5s LCP on 4G

### Database

- Every foreign key has an index
- Every column used in a WHERE clause has an index
- `EXPLAIN ANALYZE` is run on every new query that touches a table > 10k rows
- N+1 queries are eliminated via `.Include()` or projection
- Use `IQueryable.AsNoTracking()` on read-only queries
- Use database-side pagination (LIMIT/OFFSET or keyset) — never load
  full result sets into memory

### Caching strategy

- Redis cache for:
  - Session data (op_session lookups)
  - Recent AI predictions (60s TTL, keyed by input hash)
  - Dashboard stats (10s TTL per business)
- Cache invalidation: write-through, with explicit invalidation on writes

### Background jobs

- Use `BackgroundService` or Hangfire for:
  - Continuous learning ingestion (per-issue feedback)
  - Nightly analytics rollups
  - Weekly model retraining triggers
  - Email/notification dispatch
- Jobs are idempotent and retry-safe

### Scaling plan

- Backend: stateless API behind Azure App Service horizontal autoscale.
  SignalR uses Azure SignalR Service for cross-instance broadcast.
- Database: vertical scaling first; read replicas added when read load
  exceeds primary capacity
- AI service: Azure Container Apps with autoscale on request rate.
  Model files are mounted from Azure Blob; models cached in memory per pod.

---

## DEPLOYMENT PIPELINE

### Environments

- **Local** — docker-compose, all services on localhost
- **Dev** — Azure resources tagged `env=dev`, auto-deployed from `develop` branch
- **Staging** — Azure resources tagged `env=staging`, auto-deployed from `main`
- **Production** — Azure resources tagged `env=prod`, deployed via manual approval

### CI/CD pipeline (GitHub Actions)

```
PR opened → 
  lint → unit tests → integration tests → AI eval → frontend tests → 
  build images → push to Azure Container Registry → deploy to dev

Merge to main →
  rerun all tests → build → deploy to staging → smoke tests pass → 
  manual approval → deploy to production
```

### Rollout strategy

- Backend: blue-green deployment via Azure App Service deployment slots
- Frontend: atomic deploy via Azure Static Web Apps
- Database migrations: applied before backend deploy, must be backward
  compatible with the previous backend version (two-deploy pattern for
  destructive changes)
- AI models: feature-flagged. New model version deploys in shadow mode
  (predictions logged but not used) for 24h, then canary (5% traffic),
  then full rollout. Rollback is a feature flag flip.

### Observability

- **Logs:** Serilog → Azure Application Insights, structured with
  correlation IDs that span frontend → backend → AI service
- **Metrics:** App Insights metrics + custom counters for AI accuracy,
  fallback rate, override rate, average resolution time
- **Tracing:** OpenTelemetry across all services
- **Dashboards:** one ops dashboard (uptime, latency, error rate),
  one product dashboard (issues per hour, AI accuracy, satisfaction)
- **Alerts:** PagerDuty or email on:
  - 5xx rate > 1% over 5 minutes
  - AI service p99 latency > 200ms over 10 minutes
  - AI fallback rate > 10% over 1 hour
  - Database connection failures
  - Authentication failure spike

### Runbooks

Every production-impacting failure mode has a runbook in `docs/runbooks/`:
- AI service down → fallback to rules
- Database connection exhausted → restart pool, scale up
- SignalR connection storm → check rate limit, scale SignalR Service
- Migration failed in prod → rollback procedure

---

## PHASE ROADMAP & CURRENT STATE

> **Update this section when phase status changes. Always confirm
> current state via Step 0 scan before assuming.**

### Phase 0 — Scaffolding ✅
Backend solution structure, frontend Vite scaffold, CORS, AuthContext,
typed API client, base routing.

### Phase 1 — Demo Seed ✅
DemoSeedService creating Oceanview Hotel demo data on startup, idempotent.

### Phase 2 — Staff Dashboard (basic) ✅
Login page, real dashboard with stat cards, live issue feed, issue
detail modal, 10s auto-refresh.

### Phase 3 — Guest Flow 🔨 IN PROGRESS / NEXT
Mobile-first dark UI: G1 welcome with rating selection, G2 voice + text
input with green pulsing voice button, G3 status timeline with AI
suggestions, G4 resolved confirmation, G5 thank-you with Google review CTA.

### Phase 4 — AI Pipeline (initial)
Wire up AI service. Implement stages 1-5 (sentiment, classifier,
priority, router, matcher) with hosted LLM. Store predictions in
`ai_predictions`. Show AI reasoning in staff dashboard.

### Phase 5 — Real-Time (SignalR)
IssuesHub and GuestStatusHub. Replace polling with WebSocket on staff
dashboard and guest status screen.

### Phase 6 — Onboarding Flow
3-step staff signup with business type, default departments, room count.

### Phase 7 — Admin Panel + Analytics
Locations table, departments management, analytics dashboard with
recurring problem detection and AI accuracy metrics.

### Phase 8 — AI Pipeline (advanced)
Stages 6-9: solution recommender, satisfaction predictor, chatbot,
continuous learning loop.

### Phase 9 — Voice Integration
Whisper for transcription. End-to-end voice → AI pipeline → response.

### Phase 10 — Production Hardening
Security audit, load tests, observability, runbooks, Azure deployment.

### Phase 11 — Multi-Business Onboarding
Self-service signup for new hotels. Stripe billing. Admin tooling for
support team.

---

## WORKFLOW FOR EVERY TASK

**Whenever the user requests a code change, follow this exact sequence:**

1. **Acknowledge the request and identify the phase.**
   "This is part of Phase X."

2. **Run Step 0 scan.** Read every relevant file. Produce
   MISMATCHES / ALREADY EXISTS / IMPROVEMENTS summary.

3. **Propose the plan.** List files to create, modify, leave untouched.
   Note any new dependencies. Wait for user confirmation.

4. **Write the code.** Follow all rules in this document.

5. **List manual steps separately.** Package installs, migrations to
   apply, services to restart, tests to run, git commands.

6. **Provide verification steps.** Concrete commands or clicks the user
   can do to confirm the change works.

7. **Report file summary.** Files created, modified, untouched.

8. **Stop on errors.** Do not silently fix or work around errors.
   Report what failed and wait for instructions.

---

## FORBIDDEN ACTIONS

The following are never permitted. If the user asks for one of these,
push back and ask for clarification.

- Storing JWT in `localStorage` or `sessionStorage`
- Hardcoding `http://localhost:5000` anywhere in frontend source
- Modifying applied database migrations
- Skipping the Step 0 scan
- Writing placeholder code (`// TODO`, stub methods)
- Bypassing RLS for "convenience"
- Storing AI prompts inline in handler code (must be in
  `ai-service/app/pipeline/prompts/`)
- Adding secrets to `appsettings.json`, `.env` committed files, or
  source control
- Using `Console.WriteLine` for production logging
- Committing without running tests locally first
- Deploying without going through the CI/CD pipeline
- Training AI models on raw PII
- Auto-routing issues with classifier confidence below 0.7
- Using AI fallback silently — always log and tag `ai_fallback: true`
- Calling SignalR clients with sensitive data without checking group membership

---

## END OF FILE

When in doubt, re-read this file. When something seems to conflict,
ask the user. The goal is a stable, scalable, secure, AI-first product
that ships to real Albanian hotels and works flawlessly under demo
conditions.
