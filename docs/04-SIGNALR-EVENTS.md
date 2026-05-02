# OnPoint AI — SignalR Real-Time Events (v1)

Two hubs, strict event contracts, group-based fan-out.

---

## Hub 1: `StaffHub` — `/hubs/staff`

**Auth:** JWT in query string (`?access_token=`) or Authorization header during negotiation.
**Connection lifecycle:**
1. Client connects with JWT.
2. Server resolves `business_id` and `staff_user_id` from token.
3. Server adds connection to groups:
   - `business:{businessId}`
   - `business:{businessId}:user:{staffUserId}`
   - For each `dept_id` in token: `business:{businessId}:dept:{deptId}`

### Server → Client events

| Event | Payload | Targeted to |
|---|---|---|
| `feedback.created` | `FeedbackCreatedDto` | `business:{id}` |
| `feedback.classified` | `FeedbackClassifiedDto` | `business:{id}` |
| `issue.created` | `IssueDto` | `business:{id}` + `business:{id}:dept:{deptId}` |
| `issue.assigned` | `IssueAssignmentDto` | `business:{id}:user:{newAssignee}` |
| `issue.status_changed` | `IssueStatusDto` | `business:{id}` |
| `issue.commented` | `IssueCommentDto` | `business:{id}:dept:{deptId}` |
| `issue.sla_breached` | `IssueSlaDto` | `business:{id}` (Manager+ only) |
| `metrics.tick` | `DashboardMetricsDto` | `business:{id}` (every 30s) |
| `notification` | `NotificationDto` | `business:{id}:user:{userId}` |
| `redemption.created` | `RedemptionDto` | `business:{id}` |
| `redemption.claimed` | `RedemptionDto` | `business:{id}` |

### Client → Server methods

```csharp
// Optional, for "user X is typing in this issue's notes"
await connection.InvokeAsync("StartTyping", issueId);
await connection.InvokeAsync("StopTyping", issueId);

// Acknowledge a notification
await connection.InvokeAsync("MarkNotificationRead", notificationId);
```

### Payload shapes

```typescript
interface FeedbackCreatedDto {
  feedbackId: string;
  businessId: string;
  sessionId: string;
  locationId: string | null;
  locationName: string | null;
  rating: number;        // 1-5
  comment: string | null;
  categoryHint: string | null;
  classificationStatus: 'pending';
  createdAt: string;     // ISO 8601
}

interface FeedbackClassifiedDto {
  feedbackId: string;
  sentiment: 'positive' | 'neutral' | 'negative';
  categories: string[];
  severity: 'low' | 'medium' | 'high' | 'urgent';
  routedToDeptId: string | null;
}

interface IssueDto {
  id: string;
  businessId: string;
  feedbackId: string;
  sessionId: string;
  locationId: string | null;
  locationName: string | null;
  departmentId: string | null;
  departmentName: string | null;
  assignedTo: string | null;
  assigneeName: string | null;
  title: string;
  description: string;
  status: 'open' | 'assigned' | 'in_progress' | 'resolved' | 'cancelled';
  priority: 'low' | 'medium' | 'high' | 'urgent';
  createdAt: string;
  slaBreachAt: string | null;
}

interface IssueStatusDto {
  issueId: string;
  status: IssueDto['status'];
  changedBy: { id: string; name: string };
  changedAt: string;
  resolutionNote: string | null;
}

interface IssueCommentDto {
  issueId: string;
  commentId: string;
  authorId: string;
  authorName: string;
  body: string;
  isInternal: boolean;
  createdAt: string;
}

interface DashboardMetricsDto {
  activeIssues: number;
  resolvedToday: number;
  totalSessionsToday: number;
  averageScoreLast7d: number;
  asOf: string;
}

interface NotificationDto {
  id: string;
  type: string;
  title: string;
  body: string | null;
  payload: Record<string, unknown>;
  createdAt: string;
}

interface RedemptionDto {
  id: string;
  rewardName: string;
  guestUserId: string;
  guestName: string | null;
  costPoints: number;
  status: 'pending' | 'claimed';
  code: string;
  createdAt: string;
}
```

---

## Hub 2: `GuestHub` — `/hubs/guest`

**Auth:** Session cookie (HMAC-signed) OR guest JWT.
**Connection lifecycle:**
1. Client connects with `?sessionId=...` (and cookie/JWT for verification).
2. Server validates session belongs to caller.
3. Server adds connection to group: `session:{sessionId}`.
   - If guest is logged in: also `guest:{guestUserId}`.

### Server → Client events

| Event | Payload | Targeted to |
|---|---|---|
| `issue.status_changed` | `GuestIssueStatusDto` | `session:{sessionId}` |
| `issue.resolved` | `GuestIssueResolvedDto` | `session:{sessionId}` |
| `points.earned` | `PointsEarnedDto` | `session:{sessionId}` or `guest:{userId}` |
| `redemption.code_ready` | `RedemptionDto` | `guest:{userId}` |

### Payload shapes

```typescript
interface GuestIssueStatusDto {
  issueId: string;
  status: 'open' | 'assigned' | 'in_progress' | 'resolved';
  // Friendly labels for the timeline UI
  statusLabel: string;
  statusDescription: string;
  updatedAt: string;
}

interface GuestIssueResolvedDto {
  issueId: string;
  resolutionNote: string | null;
  resolvedBy: string;       // department/role label, never PII
  resolvedAt: string;
  promptForConfirmation: true;
  pointsAwardedPending: number;  // pending guest confirmation
}

interface PointsEarnedDto {
  amount: number;
  reason: string;            // 'feedback_submitted' etc.
  newBalance: number;
  businessId: string;
  businessName: string;
}
```

---

## Client integration (React)

### Install
```bash
npm i @microsoft/signalr
```

### Staff connection
```typescript
// useStaffHub.ts
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { useEffect, useRef } from 'react';

export function useStaffHub(token: string, handlers: StaffEventHandlers) {
  const connRef = useRef<HubConnection>();

  useEffect(() => {
    const conn = new HubConnectionBuilder()
      .withUrl(`${API_URL}/hubs/staff`, {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    conn.on('feedback.created', handlers.onFeedbackCreated);
    conn.on('issue.created', handlers.onIssueCreated);
    conn.on('issue.status_changed', handlers.onIssueStatusChanged);
    conn.on('issue.commented', handlers.onIssueCommented);
    conn.on('metrics.tick', handlers.onMetricsTick);
    conn.on('notification', handlers.onNotification);

    conn.start().catch(console.error);
    connRef.current = conn;

    return () => { conn.stop(); };
  }, [token]);

  return connRef;
}
```

### Guest connection
```typescript
// useGuestHub.ts — simpler, fewer events
const conn = new HubConnectionBuilder()
  .withUrl(`${API_URL}/hubs/guest?sessionId=${sessionId}`, { withCredentials: true })
  .withAutomaticReconnect()
  .build();

conn.on('issue.status_changed', updateTimeline);
conn.on('issue.resolved', showResolvedScreen);
conn.on('points.earned', flashPointsToast);
```

---

## Server-side (ASP.NET Core)

### Hub class skeleton
```csharp
[Authorize(Policy = "StaffOnly")]
public class StaffHub : Hub
{
    private readonly ITenantContext _tenant;
    private readonly ILogger<StaffHub> _log;

    public StaffHub(ITenantContext tenant, ILogger<StaffHub> log)
    {
        _tenant = tenant; _log = log;
    }

    public override async Task OnConnectedAsync()
    {
        var businessId = _tenant.BusinessId;
        var userId = Context.User!.GetStaffUserId();
        var deptIds = Context.User!.GetDepartmentIds();

        await Groups.AddToGroupAsync(Context.ConnectionId, $"business:{businessId}");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"business:{businessId}:user:{userId}");
        foreach (var d in deptIds)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"business:{businessId}:dept:{d}");

        await base.OnConnectedAsync();
    }

    public Task StartTyping(Guid issueId) =>
        Clients.OthersInGroup($"business:{_tenant.BusinessId}").SendAsync("typing.started", new { issueId, userId = Context.User!.GetStaffUserId() });
}
```

### Broadcasting (from a service, not the hub)
```csharp
public class IssueNotifier
{
    private readonly IHubContext<StaffHub> _staff;
    private readonly IHubContext<GuestHub> _guest;

    public async Task IssueStatusChanged(Issue issue, StaffUser changedBy)
    {
        var dto = new IssueStatusDto(/* ... */);

        // Staff: business-wide so Manager dashboards update
        await _staff.Clients
            .Group($"business:{issue.BusinessId}")
            .SendAsync("issue.status_changed", dto);

        // Guest: only the session that owns this issue
        await _guest.Clients
            .Group($"session:{issue.SessionId}")
            .SendAsync("issue.status_changed", new GuestIssueStatusDto(/* ... */));
    }
}
```

---

## Why two hubs (and not one)

- **Different auth paths**: staff uses JWT, guest uses session cookie. Separating hubs makes the auth filters clean.
- **Different payload shapes**: guests get sanitized DTOs (no internal staff names, no SLA info). Same event name, different payload — collapsing this into one hub is a leak risk.
- **Different connection costs**: guest connections are short-lived (a few minutes per session), staff connections are long-lived (entire shift). Different scaling dynamics.

---

## Azure SignalR Service config

In `Program.cs`:

```csharp
builder.Services
    .AddSignalR()
    .AddAzureSignalR(options =>
    {
        options.ConnectionString = builder.Configuration["AzureSignalR:ConnectionString"];
        options.ServerStickyMode = ServerStickyMode.Required;
    });
```

Then map hubs:
```csharp
app.MapHub<StaffHub>("/hubs/staff");
app.MapHub<GuestHub>("/hubs/guest");
```

That's it. Azure handles the rest.
