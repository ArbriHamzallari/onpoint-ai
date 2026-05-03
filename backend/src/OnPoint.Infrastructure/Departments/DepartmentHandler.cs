using Microsoft.EntityFrameworkCore;
using OnPoint.Domain;
using OnPoint.Infrastructure.Persistence;

namespace OnPoint.Infrastructure.Departments;

public class DepartmentHandler
{
    private readonly AppDbContext _db;

    public DepartmentHandler(AppDbContext db)
    {
        _db = db;
    }

    // ----------------------------------------------------------------
    // LIST — returns all active departments for the business (no pagination,
    // departments are always a small set)
    // ----------------------------------------------------------------
    public async Task<DepartmentListResponse> ListAsync(
        Guid businessId,
        CancellationToken ct = default)
    {
        var departments = await _db.Departments
            .AsNoTracking()
            .Where(d => d.BusinessId == businessId)
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Name)
            .ToListAsync(ct);

        var departmentIds = departments.Select(d => d.Id).ToList();

        var activeCounts = await _db.Issues
            .AsNoTracking()
            .Where(i =>
                i.BusinessId == businessId &&
                i.DepartmentId.HasValue &&
                departmentIds.Contains(i.DepartmentId!.Value) &&
                i.Status != IssueStatus.resolved &&
                i.Status != IssueStatus.cancelled)
            .GroupBy(i => i.DepartmentId!.Value)
            .Select(g => new { DepartmentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DepartmentId, x => x.Count, ct);

        var items = departments.Select(d => new DepartmentResponse(
            Id: d.Id,
            Name: d.Name,
            Description: d.Description,
            Icon: d.Icon,
            SortOrder: d.SortOrder,
            ActiveIssueCount: activeCounts.GetValueOrDefault(d.Id, 0),
            HandlesCategories: d.HandlesCategories,
            SlaMinutes: d.SlaMinutes,
            IsActive: d.IsActive,
            CreatedAt: d.CreatedAt,
            UpdatedAt: d.UpdatedAt
        )).ToList();

        return new DepartmentListResponse(items, items.Count);
    }

    // ----------------------------------------------------------------
    // GET DETAIL
    // ----------------------------------------------------------------
    public async Task<DepartmentResponse?> GetDetailAsync(
        Guid businessId,
        Guid departmentId,
        CancellationToken ct = default)
    {
        var department = await _db.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d =>
                d.Id == departmentId &&
                d.BusinessId == businessId, ct);

        if (department is null) return null;

        int activeCount = await _db.Issues
            .AsNoTracking()
            .CountAsync(i =>
                i.BusinessId == businessId &&
                i.DepartmentId == departmentId &&
                i.Status != IssueStatus.resolved &&
                i.Status != IssueStatus.cancelled, ct);

        return new DepartmentResponse(
            Id: department.Id,
            Name: department.Name,
            Description: department.Description,
            Icon: department.Icon,
            SortOrder: department.SortOrder,
            ActiveIssueCount: activeCount,
            HandlesCategories: department.HandlesCategories,
            SlaMinutes: department.SlaMinutes,
            IsActive: department.IsActive,
            CreatedAt: department.CreatedAt,
            UpdatedAt: department.UpdatedAt
        );
    }

    // ----------------------------------------------------------------
    // CREATE
    // ----------------------------------------------------------------
    public async Task<DepartmentResponse> CreateAsync(
        Guid businessId,
        CreateDepartmentRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Department name is required.");

        int maxSort = await _db.Departments
            .Where(d => d.BusinessId == businessId)
            .MaxAsync(d => (int?)d.SortOrder, ct) ?? 0;

        var now = DateTime.UtcNow;

        var handlesCategories = request.HandlesCategories ?? [];

        int slaMinutes = request.SlaMinutes is > 0
            ? request.SlaMinutes.Value
            : 60;

        var department = new Department
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Icon = request.Icon?.Trim(),
            HandlesCategories = handlesCategories,
            SlaMinutes = slaMinutes,
            SortOrder = maxSort + 1,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Departments.Add(department);
        await _db.SaveChangesAsync(ct);

        return new DepartmentResponse(
            Id: department.Id,
            Name: department.Name,
            Description: department.Description,
            Icon: department.Icon,
            SortOrder: department.SortOrder,
            ActiveIssueCount: 0,
            HandlesCategories: department.HandlesCategories,
            SlaMinutes: department.SlaMinutes,
            IsActive: department.IsActive,
            CreatedAt: department.CreatedAt,
            UpdatedAt: department.UpdatedAt
        );
    }

    // ----------------------------------------------------------------
    // UPDATE
    // ----------------------------------------------------------------
    public async Task<DepartmentResponse?> UpdateAsync(
        Guid businessId,
        Guid departmentId,
        UpdateDepartmentRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Department name is required.");

        var department = await _db.Departments
            .FirstOrDefaultAsync(d =>
                d.Id == departmentId &&
                d.BusinessId == businessId, ct);

        if (department is null) return null;

        department.Name = request.Name.Trim();
        department.Description = request.Description?.Trim();
        department.Icon = request.Icon?.Trim();
        department.SortOrder = request.SortOrder;
        if (request.HandlesCategories is not null)
            department.HandlesCategories = request.HandlesCategories;
        if (request.SlaMinutes is > 0)
            department.SlaMinutes = request.SlaMinutes.Value;
        department.IsActive = request.IsActive;
        department.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        int activeCount = await _db.Issues
            .AsNoTracking()
            .CountAsync(i =>
                i.BusinessId == businessId &&
                i.DepartmentId == departmentId &&
                i.Status != IssueStatus.resolved &&
                i.Status != IssueStatus.cancelled, ct);

        return new DepartmentResponse(
            Id: department.Id,
            Name: department.Name,
            Description: department.Description,
            Icon: department.Icon,
            SortOrder: department.SortOrder,
            ActiveIssueCount: activeCount,
            HandlesCategories: department.HandlesCategories,
            SlaMinutes: department.SlaMinutes,
            IsActive: department.IsActive,
            CreatedAt: department.CreatedAt,
            UpdatedAt: department.UpdatedAt
        );
    }

    // ----------------------------------------------------------------
    // DELETE — deactivates (IsActive = false); blocks if active issues exist.
    // ----------------------------------------------------------------
    public async Task<(bool found, bool hasActiveIssues)> DeleteAsync(
        Guid businessId,
        Guid departmentId,
        CancellationToken ct = default)
    {
        var department = await _db.Departments
            .FirstOrDefaultAsync(d =>
                d.Id == departmentId &&
                d.BusinessId == businessId, ct);

        if (department is null) return (false, false);

        bool hasActive = await _db.Issues
            .AnyAsync(i =>
                i.BusinessId == businessId &&
                i.DepartmentId == departmentId &&
                i.Status != IssueStatus.resolved &&
                i.Status != IssueStatus.cancelled, ct);

        if (hasActive) return (true, true);

        department.IsActive = false;
        department.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return (true, false);
    }
}
