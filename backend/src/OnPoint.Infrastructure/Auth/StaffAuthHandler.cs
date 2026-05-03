using Microsoft.EntityFrameworkCore;
using OnPoint.Domain;
using OnPoint.Infrastructure.Identity;
using OnPoint.Infrastructure.Persistence;

namespace OnPoint.Infrastructure.Auth;

public class StaffAuthHandler(AppDbContext db, JwtIssuer jwt)
{
    public async Task<RegisterResult> RegisterAsync(RegisterRequest req)
    {
        if (!TryParseBusinessType(req.BusinessType, out var businessType))
            return new RegisterResult(null, null, null, "INVALID_BUSINESS_TYPE");

        var emailTaken = await db.StaffUsers
            .AnyAsync(u => u.Email == req.Email.ToLowerInvariant());
        if (emailTaken)
            return new RegisterResult(null, null, null, "EMAIL_TAKEN");

        var slug = req.BusinessName
            .ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("'", "")
            .Trim('-');

        var slugTaken = await db.Businesses.AnyAsync(b => b.Slug == slug);
        if (slugTaken) slug = $"{slug}-{Guid.NewGuid().ToString()[..6]}";

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var business = new Business
            {
                Id = Guid.NewGuid(),
                Slug = slug,
                Name = req.BusinessName,
                Type = businessType,
                Plan = BusinessPlan.trial,
                Timezone = req.Timezone ?? "UTC",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Businesses.Add(business);

            var user = new StaffUser
            {
                Id = Guid.NewGuid(),
                Email = req.Email.ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
                FullName = req.FullName,
                IsEmailVerified = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.StaffUsers.Add(user);

            var membership = new BusinessMembership
            {
                Id = Guid.NewGuid(),
                BusinessId = business.Id,
                StaffUserId = user.Id,
                Role = UserRole.owner,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await db.SaveChangesAsync();

            db.BusinessMemberships.Add(membership);
            db.Departments.AddRange(GetDefaultDepartments(business.Id, businessType));
            await db.SaveChangesAsync();

            await tx.CommitAsync();

            var token = jwt.IssueStaffToken(user.Id, business.Id, UserRole.owner.ToString());
            return new RegisterResult(user.Id, business.Id, token, null);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        var user = await db.StaffUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());

        if (user is null)
            return new LoginResult(null, null, null, "INVALID_CREDENTIALS");

        if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
            return new LoginResult(null, null, null, "ACCOUNT_LOCKED");

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            await db.StaffUsers
                .Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.FailedLoginCount, u => u.FailedLoginCount + 1)
                    .SetProperty(u => u.LockedUntil,
                        u => u.FailedLoginCount >= 4
                            ? DateTime.UtcNow.AddMinutes(15)
                            : u.LockedUntil));
            return new LoginResult(null, null, null, "INVALID_CREDENTIALS");
        }

        await db.StaffUsers
            .Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.FailedLoginCount, 0)
                .SetProperty(u => u.LockedUntil, (DateTime?)null)
                .SetProperty(u => u.LastLoginAt, DateTime.UtcNow));

        var membership = await db.BusinessMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.StaffUserId == user.Id);

        if (membership is null)
            return new LoginResult(null, null, null, "NO_BUSINESS");

        var token = jwt.IssueStaffToken(user.Id, membership.BusinessId, membership.Role.ToString());
        return new LoginResult(user.Id, membership.BusinessId, token, null);
    }

    private static bool TryParseBusinessType(string value, out BusinessType type)
    {
        return Enum.TryParse(value, ignoreCase: true, out type)
               && Enum.IsDefined(type);
    }

    private static List<Department> GetDefaultDepartments(Guid businessId, BusinessType businessType)
    {
        var now = DateTime.UtcNow;
        return businessType switch
        {
            BusinessType.hotel =>
            [
                new() { Id = Guid.NewGuid(), BusinessId = businessId,
                    Name = "Front Desk",
                    Description = "Handles guest requests and general inquiries",
                    HandlesCategories = ["service", "general"],
                    SlaMinutes = 30, SortOrder = 0, CreatedAt = now, UpdatedAt = now },
                new() { Id = Guid.NewGuid(), BusinessId = businessId,
                    Name = "Housekeeping",
                    Description = "Manages room cleaning and amenities",
                    HandlesCategories = ["cleanliness", "room"],
                    SlaMinutes = 60, SortOrder = 1, CreatedAt = now, UpdatedAt = now },
                new() { Id = Guid.NewGuid(), BusinessId = businessId,
                    Name = "Maintenance",
                    Description = "Handles maintenance and technical issues",
                    HandlesCategories = ["maintenance", "technical"],
                    SlaMinutes = 120, SortOrder = 2, CreatedAt = now, UpdatedAt = now },
            ],
            BusinessType.restaurant =>
            [
                new() { Id = Guid.NewGuid(), BusinessId = businessId,
                    Name = "Front of House",
                    HandlesCategories = ["service", "food"],
                    SlaMinutes = 15, SortOrder = 0, CreatedAt = now, UpdatedAt = now },
                new() { Id = Guid.NewGuid(), BusinessId = businessId,
                    Name = "Kitchen",
                    HandlesCategories = ["food_quality"],
                    SlaMinutes = 20, SortOrder = 1, CreatedAt = now, UpdatedAt = now },
            ],
            _ =>
            [
                new() { Id = Guid.NewGuid(), BusinessId = businessId,
                    Name = "Operations",
                    HandlesCategories = ["general"],
                    SlaMinutes = 60, SortOrder = 0, CreatedAt = now, UpdatedAt = now },
            ]
        };
    }
}

public record RegisterRequest(
    string Email,
    string Password,
    string FullName,
    string BusinessName,
    string BusinessType,
    string? Timezone);

public record RegisterResult(
    Guid? UserId,
    Guid? BusinessId,
    string? AccessToken,
    string? Error);

public record LoginResult(
    Guid? UserId,
    Guid? BusinessId,
    string? AccessToken,
    string? Error);
