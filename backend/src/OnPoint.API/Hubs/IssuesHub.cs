using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace OnPoint.API.Hubs;

/// <summary>
/// Real-time issue updates for the staff dashboard. Connections authenticate via
/// JWT (Authorization header or ?access_token= query param — the latter required
/// because browsers can't set headers on WebSocket upgrade requests).
///
/// On connect, the hub reads the business_id JWT claim and joins the connection
/// to group "biz:{businessId}". Broadcasts target that group only — RLS at the
/// data layer + group scoping at the transport layer means tenant A's staff
/// cannot receive tenant B's events.
///
/// Clients listen for four server-pushed events:
///   • IssueCreated   — new issue arrived
///   • IssueUpdated   — status, AI fields, or other non-terminal change
///   • IssueAssigned  — department changed
///   • IssueResolved  — terminal state
/// Each carries the bare issueId; clients refresh from REST.
/// </summary>
[Authorize]
public sealed class IssuesHub : Hub
{
    /// <summary>
    /// Builds the SignalR group name for a business tenant. Single source of truth —
    /// the publisher uses this same helper so the names always match.
    /// </summary>
    public static string GroupFor(Guid businessId) => $"biz:{businessId}";

    public override async Task OnConnectedAsync()
    {
        var businessIdClaim = Context.User?.FindFirst("business_id")?.Value;
        if (Guid.TryParse(businessIdClaim, out var businessId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(businessId));
        }
        // No business_id claim → connection accepted but joins no group, so it
        // receives nothing. Caller would be a misconfigured client or a non-staff
        // token — not an error condition.

        await base.OnConnectedAsync();
    }
}
