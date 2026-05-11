using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OnPoint.Infrastructure.Persistence;

namespace OnPoint.API.Hubs;

/// <summary>
/// Real-time status updates for the guest-facing status screen. Auth model
/// differs from the staff hubs: there is no JWT — guests authenticate via the
/// HttpOnly <c>op_session</c> cookie. The cookie travels with the WebSocket
/// negotiate request automatically; we validate it here and reject the
/// connection (Abort) if it's missing, malformed, or points at an expired
/// session.
///
/// Tenant-scoped via group "session:{sessionId}" — a guest only ever sees
/// events for their own session, never another guest's.
/// </summary>
public sealed class GuestStatusHub : Hub
{
    /// <summary>
    /// Single source of truth for the group name. Publisher uses this so the
    /// names always match.
    /// </summary>
    public static string GroupFor(Guid sessionId) => $"session:{sessionId}";

    private readonly AppDbContext _db;

    public GuestStatusHub(AppDbContext db) => _db = db;

    public override async Task OnConnectedAsync()
    {
        var sessionId = await ResolveSessionAsync();
        if (sessionId is null)
        {
            // No valid session cookie → close the connection. The frontend
            // useGuestStatusHub hook will surface this as "disconnected" and
            // can fall back to polling or show a re-scan prompt.
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(sessionId.Value));
        await base.OnConnectedAsync();
    }

    private async Task<Guid?> ResolveSessionAsync()
    {
        // HttpContext is available during hub negotiation; we read the cookie
        // exactly like FeedbackController + TenantResolutionMiddleware do.
        var http = Context.GetHttpContext();
        if (http is null) return null;

        if (!http.Request.Cookies.TryGetValue("op_session", out var raw)
            || !Guid.TryParse(raw, out var sessionId))
        {
            return null;
        }

        var stillValid = await _db.FeedbackSessions
            .AsNoTracking()
            .AnyAsync(s => s.Id == sessionId && s.ExpiresAt > DateTime.UtcNow);

        return stillValid ? sessionId : null;
    }
}
