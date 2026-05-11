using Microsoft.AspNetCore.Mvc;
using OnPoint.Infrastructure.Sessions;

namespace OnPoint.API.Controllers;

[ApiController]
[Route("api")]
public class SessionController(GuestSessionHandler sessions) : ControllerBase
{
    [HttpPost("sessions")]
    public async Task<IActionResult> Create([FromBody] CreateSessionRequest req)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ipHash = ip is not null
            ? Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(
                        ip + DateTime.UtcNow.Date.ToString("yyyyMMdd"))))
            : null;

        var result = await sessions.CreateFromShortCodeAsync(
            req.ShortCode,
            req.Fingerprint,
            ipHash,
            Request.Headers.UserAgent.ToString(),
            geoCountry: null);

        if (result is null)
            return NotFound(new { error = "Invalid QR code" });

        Response.Cookies.Append("op_session", result.SessionId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = result.ExpiresAt
        });

        return Ok(new
        {
            sessionId    = result.SessionId,
            businessId   = result.BusinessId,
            businessName = result.BusinessName,
            businessLogoUrl = result.BusinessLogoUrl,
            location = new
            {
                id    = result.LocationId,
                name  = result.LocationName,
                label = result.LocationLabel
            },
            expiresAt = result.ExpiresAt
        });
    }

    // QR short-link entry point.
    //
    // Real phone scans open the URL in a browser — those clients send
    // Accept: text/html and get a 302 redirect to /feedback (the SPA's G1
    // welcome screen). Programmatic clients (curl, integration tests, fetch
    // without explicit Accept) get the JSON payload, which keeps existing
    // E2E scripts working.
    [HttpGet("/r/{shortCode}")]
    public async Task<IActionResult> ResolveQr(string shortCode)
    {
        var result = await sessions.CreateFromShortCodeAsync(
            shortCode, null, null,
            Request.Headers.UserAgent.ToString(), null);

        if (result is null)
            return NotFound(new { error = "Invalid QR code" });

        Response.Cookies.Append("op_session", result.SessionId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            Secure = false,
            SameSite = SameSiteMode.Lax,
            Expires = result.ExpiresAt
        });

        var acceptsHtml = Request.Headers.Accept
            .Any(h => h is not null && h.Contains("text/html", StringComparison.OrdinalIgnoreCase));

        if (acceptsHtml)
            return Redirect("/feedback");

        return Ok(new { message = "Session created", sessionId = result.SessionId });
    }

    // GET /api/sessions/me
    // Guest-scoped: returns the business + location context for the current
    // op_session cookie. The G1 welcome screen calls this on mount so it can
    // render "Welcome to Oceanview Hotel, Room 204" without needing the data
    // baked into the URL.
    [HttpGet("sessions/me")]
    public async Task<IActionResult> Me()
    {
        if (!Request.Cookies.TryGetValue("op_session", out var raw)
            || !Guid.TryParse(raw, out var sessionId))
        {
            return Unauthorized(new { error = "No valid guest session." });
        }

        var ctx = await sessions.GetContextAsync(sessionId);
        if (ctx is null)
            return NotFound(new { error = "Session expired or location unavailable." });

        return Ok(new
        {
            sessionId       = ctx.SessionId,
            businessId      = ctx.BusinessId,
            businessName    = ctx.BusinessName,
            businessLogoUrl = ctx.BusinessLogoUrl,
            location = new
            {
                id    = ctx.LocationId,
                name  = ctx.LocationName,
                label = ctx.LocationLabel
            },
            expiresAt = ctx.ExpiresAt
        });
    }
}

public record CreateSessionRequest(string ShortCode, string? Fingerprint);
