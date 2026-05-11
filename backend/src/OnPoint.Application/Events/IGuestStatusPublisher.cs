namespace OnPoint.Application.Events;

/// <summary>
/// Outbound event publisher scoped to a single guest session — broadcasts to
/// SignalR group "session:{sessionId}" so a guest sees their own issue's status
/// transitions and AI suggestions in real time.
///
/// Distinct from <see cref="IIssueEventPublisher"/>:
///   • Staff publisher scopes by business (one connection sees all tenant issues).
///   • Guest publisher scopes by session (one connection sees their issue only).
///
/// Implementations MUST NOT throw on transport failures — broadcasts are
/// best-effort and a stuck hub must never break the API request path.
/// </summary>
public interface IGuestStatusPublisher
{
    /// <summary>
    /// Fired on every status transition the guest cares about:
    /// issue created (open), assigned, started (in_progress), resolved.
    /// </summary>
    Task StatusChangedAsync(
        Guid sessionId,
        Guid issueId,
        string newStatus,
        CancellationToken ct = default);

    /// <summary>
    /// Fired after the AI pipeline finishes enriching the issue — guest can now
    /// see the AI-routed department, category, etc. on their status screen.
    /// </summary>
    Task AiUpdateAddedAsync(
        Guid sessionId,
        Guid issueId,
        CancellationToken ct = default);
}
