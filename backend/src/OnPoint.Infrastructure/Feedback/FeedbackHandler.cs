using Microsoft.EntityFrameworkCore;
using OnPoint.Application.Ai;
using OnPoint.Application.Events;
using OnPoint.Domain;
using OnPoint.Infrastructure.Persistence;

namespace OnPoint.Infrastructure.Feedback;

public class FeedbackHandler
{
    private readonly AppDbContext _db;
    private readonly FraudScorer _fraudScorer;
    private readonly PointsService _pointsService;
    private readonly IAiPipelineQueue _aiQueue;
    private readonly IIssueEventPublisher _events;
    private readonly IGuestStatusPublisher _guestEvents;

    public FeedbackHandler(
        AppDbContext db,
        FraudScorer fraudScorer,
        PointsService pointsService,
        IAiPipelineQueue aiQueue,
        IIssueEventPublisher events,
        IGuestStatusPublisher guestEvents)
    {
        _db = db;
        _fraudScorer = fraudScorer;
        _pointsService = pointsService;
        _aiQueue = aiQueue;
        _events = events;
        _guestEvents = guestEvents;
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

                // Notify subscribed staff dashboards. Publisher swallows hub errors
                // internally, but ct propagates so a cancelled request stops fast.
                await _events.IssueCreatedAsync(businessId, issueId.Value, ct);
                await _events.DashboardStatsChangedAsync(businessId, ct);

                // Notify the guest's status screen so the timeline reflects
                // "Submitted" the instant the request returns. Status here is
                // always "open" — IssueHandler transitions it later.
                await _guestEvents.StatusChangedAsync(
                    sessionId, issueId.Value, IssueStatus.open.ToString(), ct);
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

    /// <summary>
    /// Looks up the issue tied to the current guest session — used by the guest
    /// status screen. Returns null when the session has no issue (rating ≥ 4,
    /// no negative feedback → no issue created). Caller must validate the
    /// session cookie (FeedbackController does this).
    /// </summary>
    public async Task<GuestIssueStatus?> GetForSessionAsync(
        Guid sessionId, CancellationToken ct = default)
    {
        // A guest session usually has zero or one issue, but if they submit
        // multiple low-rating feedbacks in the same QR scan we want the most
        // recent one (current state of the world).
        var issue = await _db.Issues
            .AsNoTracking()
            .Where(i => i.SessionId == sessionId)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (issue is null) return null;

        string? locationName = null;
        if (issue.LocationId.HasValue)
        {
            locationName = await _db.Locations
                .AsNoTracking()
                .Where(l => l.Id == issue.LocationId.Value)
                .Select(l => l.Name)
                .FirstOrDefaultAsync(ct);
        }

        string? departmentName = null;
        if (issue.DepartmentId.HasValue)
        {
            departmentName = await _db.Departments
                .AsNoTracking()
                .Where(d => d.Id == issue.DepartmentId.Value)
                .Select(d => d.Name)
                .FirstOrDefaultAsync(ct);
        }

        return new GuestIssueStatus(
            IssueId:        issue.Id,
            Title:          issue.Title,
            Description:    issue.Description,
            Status:         issue.Status.ToString(),
            Priority:       issue.Priority.ToString(),
            LocationName:   locationName,
            DepartmentName: departmentName,
            AiCategory:     issue.AiCategory,
            AiPriorityScore: issue.AiPriorityScore,
            AiFallback:     issue.AiFallback,
            CreatedAt:      issue.CreatedAt,
            UpdatedAt:      issue.UpdatedAt,
            ResolvedAt:     issue.ResolvedAt
        );
    }
}
