using Microsoft.EntityFrameworkCore;
using OnPoint.Application.Tenancy;
using OnPoint.Infrastructure.Persistence;

namespace OnPoint.API.Middleware;

public class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task Invoke(
        HttpContext ctx,
        ITenantContext tenant,
        AppDbContext db)
    {
        // PATH 1: Staff JWT contains a business_id claim
        var businessIdClaim = ctx.User?.FindFirst("business_id")?.Value;
        if (Guid.TryParse(businessIdClaim, out var bizIdFromJwt))
        {
            tenant.SetBusiness(bizIdFromJwt);
#pragma warning disable EF1003 // Values are Guid-validated; SET LOCAL cannot be parameterized
            await db.Database.ExecuteSqlRawAsync(
                "SET LOCAL app.current_business_id = '" + bizIdFromJwt + "'");
#pragma warning restore EF1003

            if (ctx.User?.FindFirst("role")?.Value == "platform_admin")
            {
                tenant.SetPlatformAdmin();
                await db.Database.ExecuteSqlRawAsync(
                    "SET LOCAL app.is_platform_admin = 'true'");
            }
        }
        // PATH 2: Guest session cookie — look up session to find business_id
        else if (ctx.Request.Cookies.TryGetValue("op_session", out var sessionToken)
                 && Guid.TryParse(sessionToken, out var sessionId))
        {
            var session = await db.FeedbackSessions
                .AsNoTracking()
                .Where(s => s.Id == sessionId && s.ExpiresAt > DateTime.UtcNow)
                .Select(s => new { s.Id, s.BusinessId })
                .FirstOrDefaultAsync();

            if (session is not null)
            {
                tenant.SetBusiness(session.BusinessId);
                tenant.SetSession(session.Id);
#pragma warning disable EF1003 // Values are Guid-validated; SET LOCAL cannot be parameterized
                await db.Database.ExecuteSqlRawAsync(
                    "SET LOCAL app.current_business_id = '" + session.BusinessId + "'");
#pragma warning restore EF1003
            }
        }

        await next(ctx);
    }
}
