using Microsoft.EntityFrameworkCore;
using OnPoint.Application.Events;
using OnPoint.Domain;
using OnPoint.Infrastructure.Persistence;

namespace OnPoint.Infrastructure.Issues;

public class IssueHandler
{
    private const int MaxPageSize = 100;

    private readonly AppDbContext _db;
    private readonly IIssueEventPublisher _events;
    private readonly IGuestStatusPublisher _guestEvents;

    public IssueHandler(
        AppDbContext db,
        IIssueEventPublisher events,
        IGuestStatusPublisher guestEvents)
    {
        _db = db;
        _events = events;
        _guestEvents = guestEvents;
    }

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize)
    {
        var p = Math.Max(1, page);
        var ps = Math.Clamp(pageSize, 1, MaxPageSize);
        return (p, ps);
    }

    // ----------------------------------------------------------------
    // LIST
    // ----------------------------------------------------------------
    public async Task<IssueListResponse> ListAsync(
        Guid businessId,
        IssueListRequest request,
        CancellationToken ct = default)
    {
        var (page, pageSize) = NormalizePaging(request.Page, request.PageSize);

        if (!string.IsNullOrWhiteSpace(request.Status)
            && !Enum.TryParse<IssueStatus>(request.Status, ignoreCase: true, out _))
        {
            throw new ArgumentException($"Invalid status filter: {request.Status}");
        }

        var query = _db.Issues
            .AsNoTracking()
            .Where(i => i.BusinessId == businessId);

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<IssueStatus>(request.Status, ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(i => i.Status == parsedStatus);
        }

        if (request.DepartmentId.HasValue)
            query = query.Where(i => i.DepartmentId == request.DepartmentId);

        if (request.LocationId.HasValue)
            query = query.Where(i => i.LocationId == request.LocationId);

        var total = await query.CountAsync(ct);

        var issues = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var locationIds = issues
            .Where(i => i.LocationId.HasValue)
            .Select(i => i.LocationId!.Value)
            .Distinct()
            .ToList();

        var departmentIds = issues
            .Where(i => i.DepartmentId.HasValue)
            .Select(i => i.DepartmentId!.Value)
            .Distinct()
            .ToList();

        var locations = await _db.Locations
            .AsNoTracking()
            .Where(l => l.BusinessId == businessId && locationIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Name, ct);

        var departments = await _db.Departments
            .AsNoTracking()
            .Where(d => d.BusinessId == businessId && departmentIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        var items = issues.Select(i => new IssueListItem(
            IssueId: i.Id,
            Title: i.Title,
            Description: i.Description,
            Priority: i.Priority.ToString(),
            Status: i.Status.ToString(),
            LocationName: i.LocationId.HasValue
                ? locations.GetValueOrDefault(i.LocationId.Value)
                : null,
            DepartmentId: i.DepartmentId,
            DepartmentName: i.DepartmentId.HasValue
                ? departments.GetValueOrDefault(i.DepartmentId.Value)
                : null,
            CreatedAt: i.CreatedAt,
            ResolvedAt: i.ResolvedAt,
            AiCategory: i.AiCategory,
            AiCategoryConfidence: i.AiCategoryConfidence.HasValue
                ? (double?)decimal.ToDouble(i.AiCategoryConfidence.Value)
                : null,
            AiPriorityScore: i.AiPriorityScore,
            AiFallback: i.AiFallback
        )).ToList();

        return new IssueListResponse(items, total, page, pageSize);
    }

    // ----------------------------------------------------------------
    // DETAIL
    // ----------------------------------------------------------------
    public async Task<IssueDetailResponse?> GetDetailAsync(
        Guid businessId,
        Guid issueId,
        CancellationToken ct = default)
    {
        var issue = await _db.Issues
            .AsNoTracking()
            .FirstOrDefaultAsync(i =>
                i.Id == issueId && i.BusinessId == businessId, ct);

        if (issue is null)
            return null;

        var feedback = await _db.Feedbacks
            .AsNoTracking()
            .FirstOrDefaultAsync(f =>
                f.Id == issue.FeedbackId && f.BusinessId == businessId, ct);

        string? locationName = null;
        if (issue.LocationId.HasValue)
        {
            var loc = await _db.Locations
                .AsNoTracking()
                .FirstOrDefaultAsync(l =>
                    l.Id == issue.LocationId.Value && l.BusinessId == businessId, ct);
            locationName = loc?.Name;
        }

        string? departmentName = null;
        if (issue.DepartmentId.HasValue)
        {
            var dept = await _db.Departments
                .AsNoTracking()
                .FirstOrDefaultAsync(d =>
                    d.Id == issue.DepartmentId.Value && d.BusinessId == businessId, ct);
            departmentName = dept?.Name;
        }

        return new IssueDetailResponse(
            IssueId: issue.Id,
            Title: issue.Title,
            Description: issue.Description,
            Priority: issue.Priority.ToString(),
            Status: issue.Status.ToString(),
            LocationId: issue.LocationId,
            LocationName: locationName,
            DepartmentId: issue.DepartmentId,
            DepartmentName: departmentName,
            AssignedTo: issue.AssignedTo,
            ResolvedBy: issue.ResolvedBy,
            FeedbackId: issue.FeedbackId,
            FeedbackRating: feedback?.Rating ?? 0,
            FeedbackComment: feedback?.Comment,
            CreatedAt: issue.CreatedAt,
            UpdatedAt: issue.UpdatedAt,
            ResolvedAt: issue.ResolvedAt,
            AiCategory: issue.AiCategory,
            AiCategoryConfidence: issue.AiCategoryConfidence.HasValue
                ? (double?)decimal.ToDouble(issue.AiCategoryConfidence.Value)
                : null,
            AiPriorityScore: issue.AiPriorityScore,
            AiFallback: issue.AiFallback
        );
    }

    // ----------------------------------------------------------------
    // START (open | assigned → in_progress)
    // ----------------------------------------------------------------
    public async Task<IssueActionResponse> StartAsync(
        Guid businessId,
        Guid issueId,
        CancellationToken ct = default)
    {
        var issue = await _db.Issues
            .FirstOrDefaultAsync(i =>
                i.Id == issueId && i.BusinessId == businessId, ct)
            ?? throw new KeyNotFoundException("Issue not found.");

        if (issue.Status is IssueStatus.resolved or IssueStatus.cancelled)
        {
            throw new InvalidOperationException(
                $"Issue is {issue.Status} — cannot start work.");
        }

        if (issue.Status == IssueStatus.in_progress)
        {
            throw new InvalidOperationException("Issue is already in progress.");
        }

        if (issue.Status is not (IssueStatus.open or IssueStatus.assigned))
        {
            throw new InvalidOperationException(
                $"Issue status {issue.Status} cannot be started.");
        }

        issue.Status = IssueStatus.in_progress;
        issue.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _events.IssueUpdatedAsync(businessId, issue.Id, ct);
        await _events.DashboardStatsChangedAsync(businessId, ct);
        await _guestEvents.StatusChangedAsync(
            issue.SessionId, issue.Id, issue.Status.ToString(), ct);

        return new IssueActionResponse(issue.Id, issue.Status.ToString(), issue.UpdatedAt);
    }

    // ----------------------------------------------------------------
    // RESOLVE (terminal states excluded)
    // ----------------------------------------------------------------
    public async Task<IssueActionResponse> ResolveAsync(
        Guid businessId,
        Guid issueId,
        CancellationToken ct = default)
    {
        var issue = await _db.Issues
            .FirstOrDefaultAsync(i =>
                i.Id == issueId && i.BusinessId == businessId, ct)
            ?? throw new KeyNotFoundException("Issue not found.");

        if (issue.Status == IssueStatus.resolved)
            throw new InvalidOperationException("Issue is already resolved.");

        if (issue.Status == IssueStatus.cancelled)
            throw new InvalidOperationException("Issue is cancelled — cannot resolve.");

        var now = DateTime.UtcNow;
        issue.Status = IssueStatus.resolved;
        issue.ResolvedAt = now;
        issue.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        await _events.IssueResolvedAsync(businessId, issue.Id, ct);
        await _events.DashboardStatsChangedAsync(businessId, ct);
        await _guestEvents.StatusChangedAsync(
            issue.SessionId, issue.Id, issue.Status.ToString(), ct);

        return new IssueActionResponse(issue.Id, issue.Status.ToString(), issue.UpdatedAt);
    }

    // ----------------------------------------------------------------
    // ASSIGN to department
    // ----------------------------------------------------------------
    public async Task<IssueActionResponse> AssignAsync(
        Guid businessId,
        Guid issueId,
        Guid departmentId,
        CancellationToken ct = default)
    {
        var issue = await _db.Issues
            .FirstOrDefaultAsync(i =>
                i.Id == issueId && i.BusinessId == businessId, ct)
            ?? throw new KeyNotFoundException("Issue not found.");

        var departmentExists = await _db.Departments
            .AsNoTracking()
            .AnyAsync(d => d.Id == departmentId && d.BusinessId == businessId, ct);

        if (!departmentExists)
            throw new KeyNotFoundException("Department not found.");

        if (issue.Status is IssueStatus.resolved or IssueStatus.cancelled)
        {
            throw new InvalidOperationException(
                $"Cannot reassign — issue is {issue.Status}.");
        }

        issue.DepartmentId = departmentId;
        if (issue.Status == IssueStatus.open)
            issue.Status = IssueStatus.assigned;

        issue.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _events.IssueAssignedAsync(businessId, issue.Id, ct);
        await _events.DashboardStatsChangedAsync(businessId, ct);
        // Fire even when status didn't transition — the guest cares about the
        // new department name (visible on their status screen), which is part
        // of the same logical "assignment" event.
        await _guestEvents.StatusChangedAsync(
            issue.SessionId, issue.Id, issue.Status.ToString(), ct);

        return new IssueActionResponse(issue.Id, issue.Status.ToString(), issue.UpdatedAt);
    }

    // ----------------------------------------------------------------
    // DASHBOARD STATS
    // ----------------------------------------------------------------
    public async Task<DashboardStatsResponse> GetStatsAsync(
        Guid businessId,
        CancellationToken ct = default)
    {
        var todayUtc = DateTime.UtcNow.Date;

        var activeIssues = await _db.Issues
            .AsNoTracking()
            .CountAsync(i =>
                i.BusinessId == businessId
                && (i.Status == IssueStatus.open
                    || i.Status == IssueStatus.assigned
                    || i.Status == IssueStatus.in_progress), ct);

        var resolvedToday = await _db.Issues
            .AsNoTracking()
            .CountAsync(i =>
                i.BusinessId == businessId
                && i.Status == IssueStatus.resolved
                && i.ResolvedAt.HasValue
                && i.ResolvedAt.Value >= todayUtc, ct);

        var totalSessions = await _db.FeedbackSessions
            .AsNoTracking()
            .CountAsync(s =>
                s.BusinessId == businessId
                && s.ExpiresAt > DateTime.UtcNow, ct);

        double avgRating = 0;
        var hasFeedback = await _db.Feedbacks
            .AsNoTracking()
            .AnyAsync(f => f.BusinessId == businessId, ct);

        if (hasFeedback)
        {
            avgRating = await _db.Feedbacks
                .AsNoTracking()
                .Where(f => f.BusinessId == businessId)
                .AverageAsync(f => (double)f.Rating, ct);
        }

        return new DashboardStatsResponse(
            ActiveIssues: activeIssues,
            ResolvedToday: resolvedToday,
            TotalActiveSessions: totalSessions,
            AverageRating: Math.Round(avgRating, 1)
        );
    }
}
