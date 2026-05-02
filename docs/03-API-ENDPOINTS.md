# OnPoint AI — REST API Specification (v1)

**Base URL:** `https://api.onpoint.ai/v1`
**Auth:** JWT Bearer (staff/admin) · HMAC-signed session cookie (guest)
**Content-Type:** `application/json`
**Errors:** RFC 7807 Problem Details

---

## Conventions

- All IDs are UUIDs.
- All timestamps are ISO 8601 in UTC.
- Pagination: cursor-based, `?limit=20&cursor=<opaque>`.
- Soft errors return `4xx` with `{ "type", "title", "detail", "errors" }`.
- Hard errors return `5xx` with a correlation ID for support.

---

## A. Public (no auth — guest entry points)

### `GET /r/{shortCode}`
**Purpose:** Resolve a QR/short link, create a feedback session, redirect to the guest PWA.
**Response:** `302` to `https://app.onpoint.ai/feedback?s={sessionId}`, sets `op_session` cookie.

### `POST /sessions`
**Purpose:** Programmatic session creation (when PWA opens with `?s=` and needs to bootstrap).
**Body:**
```json
{ "shortCode": "abc123xyz", "fingerprint": "<fp_hash>" }
```
**Response 201:**
```json
{
  "sessionId": "uuid",
  "businessId": "uuid",
  "businessName": "Oceanview Hotel",
  "businessLogoUrl": "...",
  "location": { "id": "uuid", "name": "Room 204", "label": "Deluxe" },
  "expiresAt": "2026-05-03T12:00:00Z",
  "earningHints": [
    { "action": "feedback_submitted", "points": 10 },
    { "action": "feedback_with_comment", "points": 25 }
  ]
}
```

### `GET /sessions/me`
**Purpose:** Current session info (for PWA reload).
**Auth:** session cookie.
**Response:** same as `POST /sessions` plus `pointsEarnedThisSession`.

---

## B. Feedback (guest)

### `POST /feedback`
**Auth:** session cookie.
**Body:**
```json
{
  "rating": 2,
  "comment": "AC isn't working in the room",
  "categoryHint": "Room",
  "honeypot": ""        // must be empty; bots fill this
}
```
**Response 201:**
```json
{
  "feedbackId": "uuid",
  "issueId": "uuid",        // present if rating <= 3
  "redirectTo": null,       // present if rating >= 4 and config says redirect
  "pointsEarned": 35,
  "pendingPoints": 0,
  "classificationStatus": "pending"
}
```
**Errors:**
- `400` invalid rating
- `409` session already submitted feedback (configurable per business)
- `429` rate limit
- `403` honeypot tripped (also logs security event)

### `GET /feedback/{id}`
**Auth:** session cookie OR guest user JWT.
**Purpose:** Guest checks their own feedback status.

### `POST /issues/{id}/confirm-resolution`
**Auth:** session cookie.
**Body:**
```json
{ "satisfied": true, "rating": 5, "comment": "Thanks, fixed quickly!" }
```
**Response 200:** `{ "pointsEarned": 30, "totalPoints": 65 }`

---

## C. Account upgrade (anonymous → registered)

### `POST /guest/account/start`
**Auth:** session cookie.
**Body:**
```json
{ "email": "user@example.com" }
```
**Response 200:** `{ "verificationId": "uuid", "channel": "email", "expiresInSeconds": 600 }`
Sends a 6-digit code to email.

### `POST /guest/account/verify`
**Auth:** session cookie.
**Body:**
```json
{ "verificationId": "uuid", "code": "123456" }
```
**Response 200:**
```json
{
  "guestUserId": "uuid",
  "accessToken": "<jwt>",       // long-lived guest JWT, 30 days
  "refreshToken": "<token>",
  "pointsTransferred": 65,
  "businessesWithPoints": [
    { "businessId": "uuid", "name": "Oceanview Hotel", "points": 65 }
  ]
}
```
This endpoint:
1. Creates `guest_users` row if email is new, else loads existing.
2. Updates all `feedback_sessions` with matching device_fingerprint OR session cookie to set `guest_user_id`.
3. Issues guest JWT.
4. Triggers anti-fraud check on rapid linking.

### `POST /guest/account/login`
**Body:** `{ "email": "..." }` → sends magic link / 6-digit code.
**Then:** `POST /guest/account/login/verify` → returns JWT.

(No passwords for guests. Email/SMS code only. Reduces friction and password-reuse risk.)

### `GET /me`
**Auth:** guest JWT.
**Response:** profile + points balances per business + recent activity.

### `DELETE /me` *(GDPR)*
**Auth:** guest JWT.
**Response 202:** Triggers anonymization job; account deactivated immediately.

### `GET /me/export` *(GDPR)*
**Auth:** guest JWT.
**Response 200:** JSON file with all user data.

---

## D. Rewards (guest-facing)

### `GET /businesses/{id}/rewards`
**Auth:** session cookie OR guest JWT.
**Response:**
```json
{
  "businessId": "uuid",
  "userBalance": 65,
  "rewards": [
    {
      "id": "uuid",
      "name": "Free coffee",
      "description": "Any coffee at the lobby bar",
      "imageUrl": "...",
      "costPoints": 50,
      "stock": 12,
      "expiryDays": 30,
      "affordable": true
    }
  ]
}
```

### `POST /rewards/{id}/redeem`
**Auth:** guest JWT (REQUIRED — anonymous sessions cannot redeem).
**Response 201:**
```json
{
  "redemptionId": "uuid",
  "code": "OCN-A8F2",
  "rewardName": "Free coffee",
  "expiresAt": "2026-06-02T12:00:00Z",
  "instructions": "Show this code at the lobby bar"
}
```
**Errors:**
- `402` insufficient points
- `409` out of stock
- `429` redemption velocity (anti-fraud)

### `GET /me/redemptions`
**Auth:** guest JWT.
**Response:** list of past redemptions across all businesses.

---

## E. Auth (staff)

### `POST /auth/staff/register`
**Purpose:** Self-serve workspace creation (signup screen 1 in your Figma).
**Body:**
```json
{
  "email": "owner@hotel.com",
  "password": "...",
  "fullName": "John Smith",
  "businessName": "Oceanview Hotel",
  "businessType": "hotel",
  "timezone": "Europe/Tirane"
}
```
**Response 201:**
```json
{
  "staffUserId": "uuid",
  "businessId": "uuid",
  "accessToken": "<jwt>",
  "refreshToken": "..."
}
```
Sends email verification link async.

### `POST /auth/staff/login`
**Body:** `{ "email", "password", "totpCode?" }`
**Response 200:** `{ accessToken, refreshToken, businesses: [{...}], requiresTotp?: true }`

### `POST /auth/staff/refresh`
**Body:** `{ "refreshToken" }` → new access token.

### `POST /auth/staff/logout`
**Auth:** staff JWT. Revokes refresh token.

### `POST /auth/staff/forgot-password`
**Body:** `{ "email" }` → sends reset link.

### `POST /auth/staff/reset-password`
**Body:** `{ "token", "newPassword" }`

### `POST /auth/staff/2fa/enable`
**Auth:** staff JWT. Returns TOTP QR seed; requires verification before activation.

---

## F. Business management (staff)

All endpoints below require staff JWT. The `business_id` is resolved from the token's `business_id` claim. Staff can switch businesses via `POST /auth/staff/switch-business`.

### `GET /business`
Returns current business profile.

### `PATCH /business`
Owner/Manager only.
```json
{ "name": "...", "timezone": "...", "publicReviewLinks": {...}, "earningRules": {...} }
```

### `GET /business/setup-status`
Onboarding checklist state for the dashboard.

---

### Departments

- `GET /departments`
- `POST /departments` — `{ name, description, icon, handlesCategories[], slaMinutes }`
- `PATCH /departments/{id}`
- `DELETE /departments/{id}` (soft)

### Locations

- `GET /locations` (filters: `?type=room&search=...&page`)
- `POST /locations` — `{ name, label, type, parentId? }` (server generates `shortCode`)
- `GET /locations/{id}/qr` → returns SVG and PNG download URL
- `PATCH /locations/{id}`
- `DELETE /locations/{id}` (soft)
- `POST /locations/bulk-create` — array, for hotels onboarding 50+ rooms

### Team

- `GET /team` — list memberships
- `POST /team/invite` — `{ email, role, departmentIds? }` → sends invite email
- `PATCH /team/{membershipId}` — change role/depts
- `DELETE /team/{membershipId}`

---

## G. Feedback & issues (staff)

### `GET /feedback`
Filters: `?sentiment=&from=&to=&locationId=&deptId=&cursor=&limit=`.

### `GET /feedback/{id}`
Detail with full classification metadata.

### `GET /issues`
Filters: `?status=&priority=&deptId=&assignedTo=&search=&cursor=`.
Response includes denormalized fields for fast list rendering (matches your Live Issue Feed Figma).

### `GET /issues/{id}`
Full issue + comments + events timeline.

### `POST /issues/{id}/assign`
`{ departmentId?, assignedTo? }`

### `POST /issues/{id}/status`
`{ status: "in_progress" | "resolved", resolutionNote? }`
On `resolved`: triggers guest notification + points award (pending guest confirmation).

### `POST /issues/{id}/comments`
`{ body, isInternal: true }`

### `POST /issues/{id}/escalate`
Owner/Manager only. Bumps priority, optionally reassigns.

---

## H. Rewards (staff)

- `GET /rewards`
- `POST /rewards` — `{ name, description, imageUrl, costPoints, stock?, expiryDays, terms }`
- `PATCH /rewards/{id}`
- `DELETE /rewards/{id}` (soft)
- `GET /redemptions` (filters: `?status=pending&cursor=`)
- `POST /redemptions/{id}/claim` — staff confirms guest claimed the reward
- `POST /redemptions/{id}/cancel` — `{ reason }`, refunds points

---

## I. Analytics (staff)

### `GET /analytics/overview`
Response:
```json
{
  "activeIssues": 12,
  "resolvedToday": 24,
  "totalSessionsToday": 156,
  "averageScoreLast7d": 4.6,
  "trend": {
    "issuesPerDay": [{ "date": "2026-04-26", "count": 18 }, ...],
    "satisfactionPerDay": [...]
  }
}
```

### `GET /analytics/issues-by-department`
### `GET /analytics/resolution-time`
### `GET /analytics/feedback-volume`

All bounded to ≤90 days for v1.

---

## J. Platform admin (separate auth tenant)

Same endpoints, but `/admin/*` prefix and require `platform_admin` role.

- `GET /admin/businesses` — list all
- `POST /admin/businesses/{id}/suspend`
- `GET /admin/fraud/flagged` — review queue
- `POST /admin/fraud/flagged/{ledgerEntryId}/decision` — `{ approve | reverse }`
- `GET /admin/health` — system metrics

---

## K. Webhooks (outgoing — for businesses to integrate)

Configurable per business in settings. We POST JSON with HMAC signature header.

Events: `issue.created`, `issue.resolved`, `feedback.received`, `redemption.claimed`.

---

## Rate limits (defaults — overridable per plan)

| Endpoint group | Anonymous | Authenticated |
|---|---|---|
| `POST /sessions` | 10/min/IP | n/a |
| `POST /feedback` | 5/min/session | n/a |
| `POST /guest/account/verify` | 5/hour/IP | n/a |
| `POST /auth/staff/login` | 10/min/IP | n/a |
| Staff API (general) | n/a | 600/min/user |
| Staff API (writes) | n/a | 120/min/user |
| `POST /rewards/{id}/redeem` | n/a | 5/hour/user |

Implemented at the edge (Front Door) AND in-app via `AspNetCoreRateLimit` for defense in depth.

---

## Standard error format

```json
{
  "type": "https://onpoint.ai/errors/insufficient-points",
  "title": "Insufficient points",
  "status": 402,
  "detail": "You need 50 points to redeem this reward, but have 35.",
  "instance": "/v1/rewards/abc/redeem",
  "correlationId": "01HVABCXXX",
  "errors": {
    "balance": ["Required: 50, Available: 35"]
  }
}
```
