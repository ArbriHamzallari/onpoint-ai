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

    /// <summary>
    /// Resolves an existing session (by id from the op_session cookie) into the
    /// same shape <see cref="CreateFromShortCodeAsync"/> returns. Used by
    /// GET /api/sessions/me so the guest welcome screen can render the business
    /// name + location after a QR redirect, without re-creating the session.
    /// Returns null when the session is missing, expired, or its location was
    /// soft-deleted.
    /// </summary>
    public async Task<SessionResult?> GetContextAsync(Guid sessionId)
    {
        var session = await db.FeedbackSessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId && s.ExpiresAt > DateTime.UtcNow)
            .Select(s => new { s.Id, s.BusinessId, s.LocationId, s.ExpiresAt })
            .FirstOrDefaultAsync();

        if (session is null) return null;

        var business = await db.Businesses
            .AsNoTracking()
            .Where(b => b.Id == session.BusinessId)
            .Select(b => new { b.Id, b.Name, b.LogoUrl })
            .FirstOrDefaultAsync();

        if (business is null) return null;

        // Location is optional in the schema but always set in practice (a
        // session is always created from a QR pointing at a location). Defensive
        // null handling keeps this future-proof.
        var location = session.LocationId.HasValue
            ? await db.Locations
                .AsNoTracking()
                .Where(l => l.Id == session.LocationId.Value)
                .Select(l => new { l.Id, l.Name, l.Label })
                .FirstOrDefaultAsync()
            : null;

        return new SessionResult(
            session.Id,
            business.Id,
            business.Name,
            business.LogoUrl,
            location?.Id ?? Guid.Empty,
            location?.Name ?? string.Empty,
            location?.Label,
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
