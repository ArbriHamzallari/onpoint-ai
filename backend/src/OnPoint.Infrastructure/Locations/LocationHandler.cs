using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OnPoint.Domain;
using OnPoint.Infrastructure.Persistence;

namespace OnPoint.Infrastructure.Locations;

public class LocationHandler
{
    private readonly AppDbContext _db;
    private readonly string _baseUrl;

    public LocationHandler(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _baseUrl = (config["AppSettings:BaseUrl"] ?? "http://localhost:5000")
                   .TrimEnd('/');
    }

    // ----------------------------------------------------------------
    // LIST
    // ----------------------------------------------------------------
    public async Task<LocationListResponse> ListAsync(
        Guid businessId,
        LocationListRequest request,
        CancellationToken ct = default)
    {
        int page = Math.Max(1, request.Page);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _db.Locations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(l => l.BusinessId == businessId
                        && l.DeletedAt == null);

        if (!request.IncludeInactive)
            query = query.Where(l => l.IsActive);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(l =>
                l.Name.ToLower().Contains(request.Search.ToLower()));

        int total = await query.CountAsync(ct);

        var locations = await query
            .OrderBy(l => l.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = locations
            .Select(l => ToListItem(l))
            .ToList();

        return new LocationListResponse(items, total, page, pageSize);
    }

    // ----------------------------------------------------------------
    // GET DETAIL
    // ----------------------------------------------------------------
    public async Task<LocationDetailResponse?> GetDetailAsync(
        Guid businessId,
        Guid locationId,
        CancellationToken ct = default)
    {
        var location = await _db.Locations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(l =>
                l.Id == locationId &&
                l.BusinessId == businessId &&
                l.DeletedAt == null, ct);

        return location is null ? null : ToDetail(location);
    }

    // ----------------------------------------------------------------
    // CREATE
    // ----------------------------------------------------------------
    public async Task<LocationDetailResponse> CreateAsync(
        Guid businessId,
        CreateLocationRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Room name is required.");

        var locationType = LocationType.other;
        if (!string.IsNullOrWhiteSpace(request.Type))
            Enum.TryParse<LocationType>(request.Type,
                ignoreCase: true, out locationType);

        var shortCode = await GenerateUniqueShortCodeAsync(ct);
        var now = DateTime.UtcNow;

        var location = new Location
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Name = request.Name.Trim(),
            Label = request.Label?.Trim(),
            Type = locationType,
            ShortCode = shortCode,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Locations.Add(location);
        await _db.SaveChangesAsync(ct);

        return ToDetail(location);
    }

    // ----------------------------------------------------------------
    // UPDATE
    // ----------------------------------------------------------------
    public async Task<LocationDetailResponse?> UpdateAsync(
        Guid businessId,
        Guid locationId,
        UpdateLocationRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Room name is required.");

        var location = await _db.Locations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l =>
                l.Id == locationId &&
                l.BusinessId == businessId &&
                l.DeletedAt == null, ct);

        if (location is null) return null;

        var locationType = LocationType.other;
        if (!string.IsNullOrWhiteSpace(request.Type))
            Enum.TryParse<LocationType>(request.Type,
                ignoreCase: true, out locationType);

        location.Name = request.Name.Trim();
        location.Label = request.Label?.Trim();
        location.Type = locationType;
        location.IsActive = request.IsActive;
        location.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDetail(location);
    }

    // ----------------------------------------------------------------
    // DELETE (soft)
    // ----------------------------------------------------------------
    public async Task<(bool found, bool hasActiveIssues)> DeleteAsync(
        Guid businessId,
        Guid locationId,
        CancellationToken ct = default)
    {
        var location = await _db.Locations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l =>
                l.Id == locationId &&
                l.BusinessId == businessId &&
                l.DeletedAt == null, ct);

        if (location is null) return (false, false);

        bool hasActive = await _db.Issues
            .AnyAsync(i =>
                i.LocationId == locationId &&
                i.Status != IssueStatus.resolved &&
                i.Status != IssueStatus.cancelled, ct);

        if (hasActive) return (true, true);

        location.DeletedAt = DateTime.UtcNow;
        location.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return (true, false);
    }

    // ----------------------------------------------------------------
    // QR CODE -- returns PNG bytes
    // ----------------------------------------------------------------
    public async Task<(byte[]? png, bool found)> GetQrCodeAsync(
        Guid businessId,
        Guid locationId,
        CancellationToken ct = default)
    {
        var location = await _db.Locations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(l =>
                l.Id == locationId &&
                l.BusinessId == businessId &&
                l.DeletedAt == null, ct);

        if (location is null) return (null, false);

        var qrContent = $"{_baseUrl}/r/{location.ShortCode}";

        using var qrGenerator = new QRCoder.QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(
            qrContent,
            QRCoder.QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new QRCoder.PngByteQRCode(qrData);
        var png = qrCode.GetGraphic(10);

        return (png, true);
    }

    // ----------------------------------------------------------------
    // HELPERS
    // ----------------------------------------------------------------
    private LocationListItem ToListItem(Location l) => new(
        Id: l.Id,
        Name: l.Name,
        Label: l.Label,
        Type: l.Type.ToString(),
        ShortCode: l.ShortCode,
        GuestLink: $"{_baseUrl}/r/{l.ShortCode}",
        IsActive: l.IsActive,
        CreatedAt: l.CreatedAt
    );

    private LocationDetailResponse ToDetail(Location l) => new(
        Id: l.Id,
        Name: l.Name,
        Label: l.Label,
        Type: l.Type.ToString(),
        ShortCode: l.ShortCode,
        GuestLink: $"{_baseUrl}/r/{l.ShortCode}",
        IsActive: l.IsActive,
        CreatedAt: l.CreatedAt,
        UpdatedAt: l.UpdatedAt
    );

    private async Task<string> GenerateUniqueShortCodeAsync(
        CancellationToken ct)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        int attempts = 0;
        string code;

        do
        {
            if (++attempts > 10)
                throw new InvalidOperationException(
                    "Could not generate a unique short code.");

            code = new string(Enumerable
                .Range(0, 8)
                .Select(_ => chars[Random.Shared.Next(chars.Length)])
                .ToArray());
        }
        while (await _db.Locations
            .IgnoreQueryFilters()
            .AnyAsync(l => l.ShortCode == code, ct));

        return code;
    }
}
