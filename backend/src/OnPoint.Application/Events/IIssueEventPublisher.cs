namespace OnPoint.Application.Events;

/// <summary>
/// Outbound event publisher — broadcasts state changes to subscribed clients
/// (typically over SignalR). All methods are scoped per-business — a publish for
/// business A is never delivered to subscribers of business B.
///
/// Defined here in Application (not Infrastructure) so handlers can depend on
/// the contract without pulling in SignalR types. The production implementation
/// lives in OnPoint.API.Hubs and uses IHubContext.
///
/// Implementations MUST NOT throw on transport failures — broadcasts are
/// best-effort and a stuck hub must never break the API request path.
/// </summary>
public interface IIssueEventPublisher
{
    /// <summary>Fired by FeedbackHandler immediately after a new issue is committed.</summary>
    Task IssueCreatedAsync(Guid businessId, Guid issueId, CancellationToken ct = default);

    /// <summary>Fired for any non-terminal change: start, AI enrichment, generic update.</summary>
    Task IssueUpdatedAsync(Guid businessId, Guid issueId, CancellationToken ct = default);

    /// <summary>Fired when an issue's department changes (manual assign or AI auto-route).</summary>
    Task IssueAssignedAsync(Guid businessId, Guid issueId, CancellationToken ct = default);

    /// <summary>Fired when an issue reaches the terminal `resolved` state.</summary>
    Task IssueResolvedAsync(Guid businessId, Guid issueId, CancellationToken ct = default);

    /// <summary>Fired alongside any issue event to invalidate dashboard stat cards.</summary>
    Task DashboardStatsChangedAsync(Guid businessId, CancellationToken ct = default);
}
