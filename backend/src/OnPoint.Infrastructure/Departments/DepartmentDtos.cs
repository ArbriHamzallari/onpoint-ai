namespace OnPoint.Infrastructure.Departments;

// ---- Create ----

public record CreateDepartmentRequest(
    string Name,
    string? Description,
    string? Icon,
    string[]? HandlesCategories,
    int? SlaMinutes
);

// ---- Update ----

public record UpdateDepartmentRequest(
    string Name,
    string? Description,
    string? Icon,
    int SortOrder,
    string[]? HandlesCategories,
    int? SlaMinutes,
    bool IsActive
);

// ---- Response (used for list items and detail) ----

public record DepartmentResponse(
    Guid Id,
    string Name,
    string? Description,
    string? Icon,
    int SortOrder,
    int ActiveIssueCount,
    string[] HandlesCategories,
    int SlaMinutes,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

// ---- List ----

public record DepartmentListResponse(
    List<DepartmentResponse> Items,
    int TotalCount
);
