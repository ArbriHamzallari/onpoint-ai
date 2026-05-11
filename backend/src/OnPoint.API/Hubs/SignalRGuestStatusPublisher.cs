using Microsoft.AspNetCore.SignalR;
using OnPoint.Application.Events;

namespace OnPoint.API.Hubs;

/// <summary>
/// Production implementation of <see cref="IGuestStatusPublisher"/> backed by
/// the SignalR GuestStatusHub. All broadcasts are session-scoped via
/// <see cref="GuestStatusHub.GroupFor"/> — no cross-session leakage possible.
///
/// Best-effort delivery: every SendAsync is wrapped so a hub failure logs
/// but never propagates. CLAUDE.md §Real-Time: "The app must work even if
/// WebSocket fails."
/// </summary>
public sealed class SignalRGuestStatusPublisher : IGuestStatusPublisher
{
    private readonly IHubContext<GuestStatusHub> _hub;
    private readonly ILogger<SignalRGuestStatusPublisher> _logger;

    public SignalRGuestStatusPublisher(
        IHubContext<GuestStatusHub> hub,
        ILogger<SignalRGuestStatusPublisher> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task StatusChangedAsync(
        Guid sessionId, Guid issueId, string newStatus, CancellationToken ct = default)
    {
        try
        {
            await _hub.Clients
                .Group(GuestStatusHub.GroupFor(sessionId))
                .SendAsync("StatusChanged", new { issueId, status = newStatus }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "GuestStatusHub StatusChanged broadcast failed for session {SessionId}, issue {IssueId}.",
                sessionId, issueId);
        }
    }

    public async Task AiUpdateAddedAsync(
        Guid sessionId, Guid issueId, CancellationToken ct = default)
    {
        try
        {
            await _hub.Clients
                .Group(GuestStatusHub.GroupFor(sessionId))
                .SendAsync("AiUpdateAdded", new { issueId }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "GuestStatusHub AiUpdateAdded broadcast failed for session {SessionId}, issue {IssueId}.",
                sessionId, issueId);
        }
    }
}
