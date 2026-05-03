namespace OnPoint.Infrastructure.Issues;

// ---- List ----
public record IssueListRequest(
    string? Status,
    Guid? DepartmentId,
    Guid? LocationId,
    int Page = 1,
    int PageSize = 20
);

public record IssueListItem(
    Guid IssueId,
    string Title,
    string? Description,
    string Priority,
    string Status,
    string? LocationName,
    Guid? DepartmentId,
    string? DepartmentName,
    DateTime CreatedAt,
    DateTime? ResolvedAt
);

public record IssueListResponse(
    List<IssueListItem> Items,
    int TotalCount,
    int Page,
    int PageSize
);

// ---- Detail ----
public record IssueDetailResponse(
    Guid IssueId,
    string Title,
    string? Description,
    string Priority,
    string Status,
    Guid? LocationId,
    string? LocationName,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? AssignedTo,
    Guid? ResolvedBy,
    Guid FeedbackId,
    int FeedbackRating,
    string? FeedbackComment,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ResolvedAt
);

// ---- Actions ----
public record AssignIssueRequest(Guid DepartmentId);

public record IssueActionResponse(
    Guid IssueId,
    string Status,
    DateTime UpdatedAt
);

// ---- Dashboard stats ----
public record DashboardStatsResponse(
    int ActiveIssues,
    int ResolvedToday,
    int TotalActiveSessions,
    double AverageRating
);
