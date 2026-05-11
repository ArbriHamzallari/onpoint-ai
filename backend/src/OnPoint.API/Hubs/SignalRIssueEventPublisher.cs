using Microsoft.AspNetCore.SignalR;
using OnPoint.Application.Events;

namespace OnPoint.API.Hubs;

/// <summary>
/// Production implementation of <see cref="IIssueEventPublisher"/> backed by
/// SignalR hub contexts. Broadcasts are tenant-scoped via the per-hub group name
/// (see <see cref="IssuesHub.GroupFor"/> / <see cref="DashboardHub.GroupFor"/>).
///
/// Best-effort: every SendAsync is wrapped so a hub failure (e.g. transient
/// transport issue) is logged but does not propagate back to the handler.
/// CLAUDE.md §Real-Time: "The app must work even if WebSocket fails."
/// </summary>
public sealed class SignalRIssueEventPublisher : IIssueEventPublisher
{
    private readonly IHubContext<IssuesHub> _issues;
    private readonly IHubContext<DashboardHub> _dashboard;
    private readonly ILogger<SignalRIssueEventPublisher> _logger;

    public SignalRIssueEventPublisher(
        IHubContext<IssuesHub> issues,
        IHubContext<DashboardHub> dashboard,
        ILogger<SignalRIssueEventPublisher> logger)
    {
        _issues    = issues;
        _dashboard = dashboard;
        _logger    = logger;
    }

    public Task IssueCreatedAsync(Guid businessId, Guid issueId, CancellationToken ct = default)  =>
        SendIssueEventAsync("IssueCreated",  businessId, issueId, ct);

    public Task IssueUpdatedAsync(Guid businessId, Guid issueId, CancellationToken ct = default)  =>
        SendIssueEventAsync("IssueUpdated",  businessId, issueId, ct);

    public Task IssueAssignedAsync(Guid businessId, Guid issueId, CancellationToken ct = default) =>
        SendIssueEventAsync("IssueAssigned", businessId, issueId, ct);

    public Task IssueResolvedAsync(Guid businessId, Guid issueId, CancellationToken ct = default) =>
        SendIssueEventAsync("IssueResolved", businessId, issueId, ct);

    public async Task DashboardStatsChangedAsync(Guid businessId, CancellationToken ct = default)
    {
        try
        {
            await _dashboard.Clients
                .Group(DashboardHub.GroupFor(businessId))
                .SendAsync("StatsChanged", ct);
        }
        catch (Exception ex)
        {
            // Best-effort: never fail the request because of a hub broadcast.
            _logger.LogWarning(ex,
                "DashboardHub broadcast failed for business {BusinessId}.", businessId);
        }
    }

    private async Task SendIssueEventAsync(
        string eventName, Guid businessId, Guid issueId, CancellationToken ct)
    {
        try
        {
            // Payload is the bare issueId — clients refetch from REST to render.
            // Keeps the wire shape stable and dashboard logic in one place.
            await _issues.Clients
                .Group(IssuesHub.GroupFor(businessId))
                .SendAsync(eventName, new { issueId }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "IssuesHub {Event} broadcast failed for business {BusinessId}, issue {IssueId}.",
                eventName, businessId, issueId);
        }
    }
}
