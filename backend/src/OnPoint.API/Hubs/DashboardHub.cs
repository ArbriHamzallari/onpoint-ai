using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace OnPoint.API.Hubs;

/// <summary>
/// Real-time dashboard stat-card invalidation. Separate hub from IssuesHub so a
/// page that only needs stats can subscribe without paying for the issue stream
/// (and vice-versa).
///
/// Single broadcast event: <c>StatsChanged</c> — payload is empty; the client
/// refetches GET /api/dashboard/stats. We keep payloads thin to avoid duplicating
/// dashboard math in two places.
/// </summary>
[Authorize]
public sealed class DashboardHub : Hub
{
    public static string GroupFor(Guid businessId) => $"biz:{businessId}";

    public override async Task OnConnectedAsync()
    {
        var businessIdClaim = Context.User?.FindFirst("business_id")?.Value;
        if (Guid.TryParse(businessIdClaim, out var businessId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(businessId));
        }
        await base.OnConnectedAsync();
    }
}
