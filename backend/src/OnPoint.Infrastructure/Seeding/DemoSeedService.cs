using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OnPoint.Domain;
using OnPoint.Infrastructure.Persistence;
using FeedbackEntity = OnPoint.Domain.Feedback;

namespace OnPoint.Infrastructure.Seeding;

public class DemoSeedService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DemoSeedService> _logger;

    public DemoSeedService(IServiceProvider services, ILogger<DemoSeedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Idempotent on demo login — avoids skipping when an older "Oceanview Hotel"
        // row exists without demo staff (slug is unique per demo business).
        var alreadySeeded = await db.StaffUsers
            .AnyAsync(u => u.Email == "demo@onpoint.ai", cancellationToken);

        if (alreadySeeded)
        {
            _logger.LogInformation("Demo seed: demo@onpoint.ai already exists, skipping.");
            return;
        }

        _logger.LogInformation("Demo seed: Seeding Oceanview Hotel demo data...");

        var business = new Business
        {
            Id        = Guid.NewGuid(),
            Slug      = "oceanview-demo",
            Name      = "Oceanview Hotel",
            Type      = BusinessType.hotel,
            Plan      = BusinessPlan.trial,
            Timezone  = "Europe/Tirane",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Businesses.Add(business);

        var staffUser = new StaffUser
        {
            Id           = Guid.NewGuid(),
            Email        = "demo@onpoint.ai",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("demo123"),
            FullName     = "John Smith",
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        };
        db.StaffUsers.Add(staffUser);

        var membership = new BusinessMembership
        {
            Id           = Guid.NewGuid(),
            StaffUserId  = staffUser.Id,
            BusinessId   = business.Id,
            Role         = UserRole.owner,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        };
        db.BusinessMemberships.Add(membership);

        var deptFrontDesk = new Department
        {
            Id                = Guid.NewGuid(),
            BusinessId        = business.Id,
            Name              = "Front Desk",
            Description       = "Handles guest requests and general inquiries.",
            Icon              = "concierge-bell",
            SortOrder         = 1,
            HandlesCategories = ["service", "other"],
            SlaMinutes        = 30,
            IsActive          = true,
            CreatedAt         = DateTime.UtcNow,
            UpdatedAt         = DateTime.UtcNow,
        };
        var deptHousekeeping = new Department
        {
            Id                = Guid.NewGuid(),
            BusinessId        = business.Id,
            Name              = "Housekeeping",
            Description       = "Manages room cleaning and amenities.",
            Icon              = "sparkles",
            SortOrder         = 2,
            HandlesCategories = ["cleanliness", "room"],
            SlaMinutes        = 45,
            IsActive          = true,
            CreatedAt         = DateTime.UtcNow,
            UpdatedAt         = DateTime.UtcNow,
        };
        var deptMaintenance = new Department
        {
            Id                = Guid.NewGuid(),
            BusinessId        = business.Id,
            Name              = "Maintenance",
            Description       = "Handles maintenance and technical issues.",
            Icon              = "wrench",
            SortOrder         = 3,
            HandlesCategories = ["maintenance"],
            SlaMinutes        = 60,
            IsActive          = true,
            CreatedAt         = DateTime.UtcNow,
            UpdatedAt         = DateTime.UtcNow,
        };
        db.Departments.AddRange(deptFrontDesk, deptHousekeeping, deptMaintenance);

        var roomLabels = new[]
        {
            "Deluxe", "Deluxe", "Suite", "Deluxe", "Standard",
            "Suite", "Standard", "Deluxe", "Suite", "Standard",
        };
        var rooms = new List<Location>();
        for (int i = 0; i < 10; i++)
        {
            var room = new Location
            {
                Id         = Guid.NewGuid(),
                BusinessId = business.Id,
                Name       = $"Room {101 + i}",
                Label      = roomLabels[i],
                Type       = LocationType.room,
                ShortCode  = GenerateShortCode(),
                IsActive   = true,
                ParentId   = null,
                DeletedAt  = null,
                CreatedAt  = DateTime.UtcNow,
                UpdatedAt  = DateTime.UtcNow,
            };
            rooms.Add(room);
            db.Locations.Add(room);
        }

        var devRoom = new Location
        {
            Id         = Guid.NewGuid(),
            BusinessId = business.Id,
            Name       = "Room 204",
            Label      = "Deluxe",
            Type       = LocationType.room,
            ShortCode  = "test-qr-001",
            IsActive   = true,
            ParentId   = null,
            DeletedAt  = null,
            CreatedAt  = DateTime.UtcNow,
            UpdatedAt  = DateTime.UtcNow,
        };

        var shortCodeExists = await db.Locations
            .IgnoreQueryFilters()
            .AnyAsync(l => l.ShortCode == "test-qr-001", cancellationToken);
        if (!shortCodeExists)
        {
            rooms.Add(devRoom);
            db.Locations.Add(devRoom);
        }

        // Single transaction so SET LOCAL app.current_business_id applies to every INSERT (RLS).
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
        await SetTenantBusinessAsync(db, business.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var rng = new Random(42);

        var issueData = new[]
        {
            (0, 2, "Air conditioner is not working properly. It's very warm in the room.", "room", "high", "open", 2),
            (2, 1, "No hot water in the shower. Tried for 10 minutes but still nothing.", "room", "urgent", "open", 7),
            (4, 2, "Housekeeping did not clean the room this morning.", "cleanliness", "high", "in_progress", 32),
            (6, 2, "Toilet is running constantly and making noise.", "room", "medium", "in_progress", 5),
            (1, 1, "TV remote is not working. Tried new batteries already.", "room", "low", "open", 15),
            (3, 2, "Wifi is very slow, barely able to load a webpage.", "service", "high", "open", 20),
            (5, 2, "Room smells musty, needs to be aired out.", "cleanliness", "medium", "assigned", 45),
            (7, 1, "Minibar is not stocked despite being listed as included.", "service", "low", "assigned", 60),
            (8, 2, "Window blind is broken and won't close fully.", "room", "medium", "in_progress", 90),
            (9, 1, "Noisy pipes in the wall, keeps us awake at night.", "room", "high", "open", 10),
            (0, 2, "Light bulb in bathroom is out.", "maintenance", "low", "resolved", 180),
            (2, 2, "Door lock is stiff and hard to open with the key card.", "maintenance", "medium", "resolved", 240),
            (4, 1, "Shower drain is very slow.", "room", "medium", "resolved", 300),
            (6, 2, "Extra pillows requested, none were brought.", "service", "low", "resolved", 360),
            (1, 2, "Iron in the room is not heating up properly.", "room", "low", "resolved", 420),
        };

        var positiveData = new[]
        {
            (3, 5, "Absolutely wonderful stay! The room was spotless.", "service", 480),
            (5, 5, "Staff were incredibly helpful and friendly.", "service", 500),
            (7, 4, "Great location and comfortable bed.", (string?)null, 520),
            (8, 4, "Very clean room, will definitely come back.", "cleanliness", 540),
            (9, 5, "Perfect experience from check-in to check-out.", "service", 560),
        };

        var pendingIssues = new List<(
            FeedbackSession Session,
            Location Room,
            int Rating,
            string Comment,
            string Category,
            string PriorityStr,
            string StatusStr,
            DateTime CreatedAt)>();

        foreach (var (roomIdx, rating, comment, category, priorityStr, statusStr, minutesAgo) in issueData)
        {
            var room = rooms[Math.Min(roomIdx, rooms.Count - 1)];
            var createdAt = now.AddMinutes(-minutesAgo);

            var session = new FeedbackSession
            {
                Id           = Guid.NewGuid(),
                BusinessId   = business.Id,
                LocationId   = room.Id,
                StartedAt    = createdAt,
                LastActiveAt = createdAt,
                ExpiresAt    = createdAt.AddHours(24),
            };
            db.FeedbackSessions.Add(session);
            pendingIssues.Add((session, room, rating, comment, category, priorityStr, statusStr, createdAt));
        }

        var pendingPositive = new List<(
            FeedbackSession Session,
            Location Room,
            int Rating,
            string Comment,
            string? Category,
            DateTime CreatedAt)>();

        foreach (var (roomIdx, rating, comment, category, minutesAgo) in positiveData)
        {
            var room = rooms[Math.Min(roomIdx, rooms.Count - 1)];
            var createdAt = now.AddMinutes(-minutesAgo);

            var session = new FeedbackSession
            {
                Id           = Guid.NewGuid(),
                BusinessId   = business.Id,
                LocationId   = room.Id,
                StartedAt    = createdAt,
                LastActiveAt = createdAt,
                ExpiresAt    = createdAt.AddHours(24),
            };
            db.FeedbackSessions.Add(session);
            pendingPositive.Add((session, room, rating, comment, category, createdAt));
        }

        // Persist sessions first so feedback FK (session_id) is satisfied regardless of batch order.
        await db.SaveChangesAsync(cancellationToken);

        foreach (var (session, room, rating, comment, category, priorityStr, statusStr, createdAt) in pendingIssues)
        {
            var feedback = new FeedbackEntity
            {
                Id                   = Guid.NewGuid(),
                SessionId            = session.Id,
                BusinessId           = business.Id,
                LocationId           = room.Id,
                Rating               = rating,
                Comment              = comment,
                CategoryHint         = category,
                ClassificationStatus = "completed",
                CreatedAt            = createdAt,
                UpdatedAt            = createdAt,
            };
            db.Feedbacks.Add(feedback);

            var resolvedAt = statusStr == "resolved"
                ? (DateTime?)createdAt.AddMinutes(rng.Next(15, 60))
                : null;

            var deptForIssue = category switch
            {
                "cleanliness" => deptHousekeeping,
                "maintenance" => deptMaintenance,
                _             => deptFrontDesk,
            };

            var issueStatus = ParseIssueStatus(statusStr);
            var issuePriority = ParseIssuePriority(priorityStr);

            var issue = new Issue
            {
                Id           = Guid.NewGuid(),
                FeedbackId   = feedback.Id,
                SessionId    = session.Id,
                BusinessId   = business.Id,
                LocationId   = room.Id,
                DepartmentId = issueStatus == IssueStatus.open ? null : deptForIssue.Id,
                Status       = issueStatus,
                Priority     = issuePriority,
                Title        = comment.Length > 60 ? comment[..57] + "..." : comment,
                Description  = comment,
                AssignedTo   = null,
                ResolvedBy   = null,
                CreatedAt    = createdAt,
                UpdatedAt    = resolvedAt ?? createdAt,
                ResolvedAt   = resolvedAt,
            };
            db.Issues.Add(issue);
        }

        foreach (var (session, room, rating, comment, category, createdAt) in pendingPositive)
        {
            var feedback = new FeedbackEntity
            {
                Id                   = Guid.NewGuid(),
                SessionId            = session.Id,
                BusinessId           = business.Id,
                LocationId           = room.Id,
                Rating               = rating,
                Comment              = comment,
                CategoryHint         = category,
                ClassificationStatus = "completed",
                CreatedAt            = createdAt,
                UpdatedAt            = createdAt,
            };
            db.Feedbacks.Add(feedback);
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        _logger.LogInformation(
            "Demo seed: Complete. {RoomCount} rooms, 3 departments, 15 issues, 5 positive feedback entries created.",
            rooms.Count);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Tenant-scoped tables use RLS (<c>business_id = current_business_id()</c>). Middleware sets this per HTTP request;
    /// the seeder must set it on the connection before SaveChanges.
    /// </summary>
    private static Task SetTenantBusinessAsync(
        AppDbContext db,
        Guid businessId,
        CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlRawAsync(
            $"SET LOCAL app.current_business_id = '{businessId}'",
            cancellationToken);

    private static IssueStatus ParseIssueStatus(string s) => s switch
    {
        "assigned"    => IssueStatus.assigned,
        "in_progress" => IssueStatus.in_progress,
        "resolved"    => IssueStatus.resolved,
        _             => IssueStatus.open,
    };

    private static IssuePriority ParseIssuePriority(string p) => p switch
    {
        "high"   => IssuePriority.high,
        "urgent" => IssuePriority.urgent,
        "medium" => IssuePriority.medium,
        "low"    => IssuePriority.low,
        _        => IssuePriority.medium,
    };

    private static string GenerateShortCode()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var rng = new Random();
        return new string(Enumerable.Range(0, 8)
            .Select(_ => chars[rng.Next(chars.Length)])
            .ToArray());
    }
}
