namespace OnPoint.Infrastructure.Locations;

// ---- Create ----

public record CreateLocationRequest(
    string Name,
    string? Label,
    string? Type
);

// ---- Update ----

public record UpdateLocationRequest(
    string Name,
    string? Label,
    string? Type,
    bool IsActive
);

// ---- List ----

public record LocationListRequest(
    string? Search,
    bool IncludeInactive = false,
    int Page = 1,
    int PageSize = 20
);

public record LocationListItem(
    Guid Id,
    string Name,
    string? Label,
    string Type,
    string ShortCode,
    string GuestLink,
    bool IsActive,
    DateTime CreatedAt
);

public record LocationListResponse(
    List<LocationListItem> Items,
    int TotalCount,
    int Page,
    int PageSize
);

// ---- Detail (returned by create, update, get) ----

public record LocationDetailResponse(
    Guid Id,
    string Name,
    string? Label,
    string Type,
    string ShortCode,
    string GuestLink,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
