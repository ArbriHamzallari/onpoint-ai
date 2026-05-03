using Microsoft.EntityFrameworkCore;
using OnPoint.Domain;
using OnPoint.Infrastructure.Persistence;

namespace OnPoint.Infrastructure.Sessions;

public class GuestSessionHandler(AppDbContext db)
{
    public async Task<SessionResult?> CreateFromShortCodeAsync(
        string shortCode,
        string? fingerprintHash,
        string? ipHash,
        string? userAgent,
        string? geoCountry)
    {
        var location = await db.Locations
            .AsNoTracking()
            .Where(l => l.ShortCode == shortCode)
            .Select(l => new { l.Id, l.BusinessId, l.Name, l.Label })
            .FirstOrDefaultAsync();

        if (location is null) return null;

        var business = await db.Businesses
            .AsNoTracking()
            .Where(b => b.Id == location.BusinessId)
            .Select(b => new { b.Id, b.Name, b.LogoUrl })
            .FirstOrDefaultAsync();

        if (business is null) return null;

        var session = new FeedbackSession
        {
            Id = Guid.NewGuid(),
            BusinessId = location.BusinessId,
            LocationId = location.Id,
            DeviceFingerprintHash = fingerprintHash,
            IpHash = ipHash,
            UserAgent = userAgent,
            GeoCountry = geoCountry,
            StartedAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            FraudScore = 0
        };

        db.FeedbackSessions.Add(session);
        await db.SaveChangesAsync();

        return new SessionResult(
            session.Id,
            business.Id,
            business.Name,
            business.LogoUrl,
            location.Id,
            location.Name,
            location.Label,
            session.ExpiresAt);
    }
}

public record SessionResult(
    Guid SessionId,
    Guid BusinessId,
    string BusinessName,
    string? BusinessLogoUrl,
    Guid LocationId,
    string LocationName,
    string? LocationLabel,
    DateTime ExpiresAt);
