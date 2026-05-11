using Microsoft.EntityFrameworkCore;
using OnPoint.Application.Ai;
using OnPoint.Domain;
using OnPoint.Infrastructure.Persistence;

namespace OnPoint.Infrastructure.Feedback;

public class FeedbackHandler
{
    private readonly AppDbContext _db;
    private readonly FraudScorer _fraudScorer;
    private readonly PointsService _pointsService;
    private readonly IAiPipelineQueue _aiQueue;

    public FeedbackHandler(
        AppDbContext db,
        FraudScorer fraudScorer,
        PointsService pointsService,
        IAiPipelineQueue aiQueue)
    {
        _db = db;
        _fraudScorer = fraudScorer;
        _pointsService = pointsService;
        _aiQueue = aiQueue;
    }

    public async Task<SubmitFeedbackResponse> HandleAsync(
        SubmitFeedbackRequest request,
        Guid sessionId,
        CancellationToken ct = default)
    {
        if (request.Rating < 1 || request.Rating > 5)
            throw new ArgumentException("Rating must be between 1 and 5.");

        var session = await _db.FeedbackSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new InvalidOperationException("Session not found.");

        if (session.ExpiresAt <= DateTime.UtcNow)
            throw new InvalidOperationException("Session has expired.");

        var businessId = session.BusinessId;

        int fraudScore = await _fraudScorer.ScoreAsync(sessionId, request.Website);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var now = DateTime.UtcNow;

            var feedback = new Domain.Feedback
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                BusinessId = businessId,
                LocationId = session.LocationId,
                Rating = request.Rating,
                Comment = request.Comment,
                CategoryHint = request.CategoryHint,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.Feedbacks.Add(feedback);

            Guid? issueId = null;
            string? redirectUrl = null;
            bool isNegative = request.Rating <= 3 || fraudScore >= 30;

            if (isNegative)
            {
                var department = await _db.Departments
                    .Where(d => d.BusinessId == businessId)
                    .OrderBy(d => d.SortOrder)
                    .ThenBy(d => d.CreatedAt)
                    .FirstOrDefaultAsync(ct);

                var priority = request.Rating switch
                {
                    1 => IssuePriority.high,
                    2 => IssuePriority.medium,
                    _ => IssuePriority.low
                };

                var titleText = string.IsNullOrEmpty(request.Comment)
                    ? $"Guest issue \u2014 rating {request.Rating}/5"
                    : request.Comment.Length > 80
                        ? request.Comment[..80] + "\u2026"
                        : request.Comment;

                var issue = new Issue
                {
                    Id = Guid.NewGuid(),
                    FeedbackId = feedback.Id,
                    SessionId = sessionId,
                    BusinessId = businessId,
                    LocationId = session.LocationId,
                    DepartmentId = department?.Id,
                    Status = IssueStatus.open,
                    Priority = priority,
                    Title = titleText,
                    Description = request.Comment,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _db.Issues.Add(issue);
                issueId = issue.Id;
            }
            else
            {
                var business = await _db.Businesses
                    .FirstOrDefaultAsync(b => b.Id == businessId, ct);

                if (business?.PublicReviewLinks is { Count: > 0 } links)
                {
                    redirectUrl = links.GetValueOrDefault("google")
                               ?? links.Values.First();
                }
            }

            int pointsEarned = _pointsService.Award(
                sessionId, businessId, feedback.Id,
                request.Comment, fraudScore);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            // Enqueue AI inference AFTER commit — issue must exist in DB before
            // the background worker reads it. Fire-and-forget; guest response is
            // already in-flight. Per CLAUDE.md: never block the guest path.
            if (issueId.HasValue)
            {
                var pipelineText = string.IsNullOrWhiteSpace(request.Comment)
                    ? $"Guest issue — rating {request.Rating}/5"
                    : request.Comment.Trim();

                _aiQueue.Enqueue(new AiPipelineRequest(
                    BusinessId:  businessId,
                    SessionId:   sessionId,
                    IssueId:     issueId.Value,
                    FeedbackId:  feedback.Id,
                    Text:        pipelineText,
                    Rating:      request.Rating
                ));
            }

            return new SubmitFeedbackResponse(
                FeedbackId: feedback.Id,
                IssueId: issueId,
                PointsEarned: pointsEarned,
                RedirectUrl: redirectUrl
            );
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
