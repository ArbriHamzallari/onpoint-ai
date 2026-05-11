namespace OnPoint.Application.Events;

/// <summary>
/// No-op guest status publisher for unit tests and environments without SignalR.
/// Singleton via <see cref="Instance"/> avoids allocations.
/// </summary>
public sealed class NullGuestStatusPublisher : IGuestStatusPublisher
{
    public static readonly NullGuestStatusPublisher Instance = new();

    private NullGuestStatusPublisher() { }

    public Task StatusChangedAsync(
        Guid sessionId, Guid issueId, string newStatus, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task AiUpdateAddedAsync(
        Guid sessionId, Guid issueId, CancellationToken ct = default)
        => Task.CompletedTask;
}
