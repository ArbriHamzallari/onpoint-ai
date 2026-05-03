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

    // QR short-link entry point — will redirect to PWA in Phase 5
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

        // Phase 5: replace with Redirect($"https://app.onpoint.ai/feedback?s={result.SessionId}")
        return Ok(new { message = "Session created", sessionId = result.SessionId });
    }
}

public record CreateSessionRequest(string ShortCode, string? Fingerprint);
