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
