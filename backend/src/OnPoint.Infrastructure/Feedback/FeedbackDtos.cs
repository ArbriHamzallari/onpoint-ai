namespace OnPoint.Infrastructure.Feedback;

public record SubmitFeedbackRequest(
    int Rating,
    string? Comment,
    string? CategoryHint,
    string? Website
);

public record SubmitFeedbackResponse(
    Guid FeedbackId,
    Guid? IssueId,
    int PointsEarned,
    string? RedirectUrl
);

// Returned by GET /api/feedback/me/issue — guest's view of their own issue.
// Deliberately omits staff-only fields (assignedTo, resolvedBy, internal notes).
public record GuestIssueStatus(
    Guid IssueId,
    string Title,
    string? Description,
    string Status,
    string Priority,
    string? LocationName,
    string? DepartmentName,
    string? AiCategory,
    int? AiPriorityScore,
    bool AiFallback,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ResolvedAt
);
