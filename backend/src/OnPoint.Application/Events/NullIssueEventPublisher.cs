namespace OnPoint.Application.Events;

/// <summary>
/// No-op event publisher for unit tests and any environment where SignalR is
/// not wired up. Singleton instance via <see cref="Instance"/> avoids allocations.
/// </summary>
public sealed class NullIssueEventPublisher : IIssueEventPublisher
{
    public static readonly NullIssueEventPublisher Instance = new();

    private NullIssueEventPublisher() { }

    public Task IssueCreatedAsync(Guid businessId, Guid issueId, CancellationToken ct = default)  => Task.CompletedTask;
    public Task IssueUpdatedAsync(Guid businessId, Guid issueId, CancellationToken ct = default)  => Task.CompletedTask;
    public Task IssueAssignedAsync(Guid businessId, Guid issueId, CancellationToken ct = default) => Task.CompletedTask;
    public Task IssueResolvedAsync(Guid businessId, Guid issueId, CancellationToken ct = default) => Task.CompletedTask;
    public Task DashboardStatsChangedAsync(Guid businessId, CancellationToken ct = default)       => Task.CompletedTask;
}
