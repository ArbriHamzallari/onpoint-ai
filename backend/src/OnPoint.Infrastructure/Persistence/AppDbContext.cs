using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OnPoint.Domain;

namespace OnPoint.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<FeedbackSession> FeedbackSessions => Set<FeedbackSession>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<StaffUser> StaffUsers => Set<StaffUser>();
    public DbSet<BusinessMembership> BusinessMemberships => Set<BusinessMembership>();
    public DbSet<Department> Departments => Set<Department>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Map to exact PostgreSQL table names (snake_case)
        modelBuilder.Entity<Business>().ToTable("businesses");
        modelBuilder.Entity<Location>().ToTable("locations");
        modelBuilder.Entity<FeedbackSession>().ToTable("feedback_sessions");
        modelBuilder.Entity<Feedback>().ToTable("feedback");
        modelBuilder.Entity<Issue>().ToTable("issues");
        modelBuilder.Entity<StaffUser>().ToTable("staff_users");
        modelBuilder.Entity<BusinessMembership>().ToTable("business_memberships");
        modelBuilder.Entity<Department>().ToTable("departments");

        // Business
        modelBuilder.Entity<Business>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Slug).HasColumnName("slug");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Type).HasColumnName("type");
            e.Property(x => x.Plan).HasColumnName("plan");
            e.Property(x => x.Timezone).HasColumnName("timezone");
            e.Property(x => x.Locale).HasColumnName("locale");
            e.Property(x => x.LogoUrl).HasColumnName("logo_url");
            e.Property(x => x.TrialEndsAt).HasColumnName("trial_ends_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            e.Property(x => x.PublicReviewLinks)
                .HasColumnName("public_review_links")
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new());
            e.Property(x => x.EarningRules)
                .HasColumnName("earning_rules")
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new());
            e.Property(x => x.Settings)
                .HasColumnName("settings")
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new());
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        // Location
        modelBuilder.Entity<Location>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.BusinessId).HasColumnName("business_id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Label).HasColumnName("label");
            e.Property(x => x.Type).HasColumnName("type");
            e.Property(x => x.ShortCode).HasColumnName("short_code");
            e.Property(x => x.ParentId).HasColumnName("parent_id");
            e.Property(x => x.IsActive).HasColumnName("is_active");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            e.HasQueryFilter(x => x.DeletedAt == null && x.IsActive);
        });

        // FeedbackSession
        modelBuilder.Entity<FeedbackSession>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.BusinessId).HasColumnName("business_id");
            e.Property(x => x.LocationId).HasColumnName("location_id");
            e.Property(x => x.GuestUserId).HasColumnName("guest_user_id");
            e.Property(x => x.DeviceFingerprintHash).HasColumnName("device_fingerprint_hash");
            e.Property(x => x.IpHash).HasColumnName("ip_hash");
            e.Property(x => x.UserAgent).HasColumnName("user_agent");
            e.Property(x => x.GeoCountry).HasColumnName("geo_country");
            e.Property(x => x.StartedAt).HasColumnName("started_at");
            e.Property(x => x.LastActiveAt).HasColumnName("last_active_at");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.FraudScore).HasColumnName("fraud_score");
        });

        // Feedback
        modelBuilder.Entity<Feedback>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.BusinessId).HasColumnName("business_id");
            e.Property(x => x.SessionId).HasColumnName("session_id");
            e.Property(x => x.LocationId).HasColumnName("location_id");
            e.Property(x => x.Rating).HasColumnName("rating");
            e.Property(x => x.Comment).HasColumnName("comment");
            e.Property(x => x.CategoryHint).HasColumnName("category_hint");
            e.Property(x => x.ClassificationStatus).HasColumnName("classification_status");
            e.Property(x => x.Sentiment).HasColumnName("sentiment");
            e.Property(x => x.Categories).HasColumnName("categories");
            e.Property(x => x.Severity).HasColumnName("severity");
            e.Property(x => x.RoutedToDeptId).HasColumnName("routed_to_dept_id");
            e.Property(x => x.RedirectedToPublic).HasColumnName("redirected_to_public");
            e.Property(x => x.ContainsPii).HasColumnName("contains_pii");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // Issue
        modelBuilder.Entity<Issue>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.BusinessId).HasColumnName("business_id");
            e.Property(x => x.FeedbackId).HasColumnName("feedback_id");
            e.Property(x => x.SessionId).HasColumnName("session_id");
            e.Property(x => x.LocationId).HasColumnName("location_id");
            e.Property(x => x.DepartmentId).HasColumnName("department_id");
            e.Property(x => x.AssignedTo).HasColumnName("assigned_to");
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.Priority).HasColumnName("priority");
            e.Property(x => x.ResolutionNote).HasColumnName("resolution_note");
            e.Property(x => x.ResolvedBy).HasColumnName("resolved_by");
            e.Property(x => x.ResolvedAt).HasColumnName("resolved_at");
            e.Property(x => x.GuestConfirmedResolution).HasColumnName("guest_confirmed_resolution");
            e.Property(x => x.SlaBreachAt).HasColumnName("sla_breach_at");
            e.Property(x => x.SlaBreached).HasColumnName("sla_breached");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // StaffUser
        modelBuilder.Entity<StaffUser>(e =>
        {
            e.ToTable("staff_users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.PasswordHash).HasColumnName("password_hash");
            e.Property(x => x.FullName).HasColumnName("full_name");
            e.Property(x => x.AvatarUrl).HasColumnName("avatar_url");
            e.Property(x => x.IsEmailVerified).HasColumnName("is_email_verified");
            e.Property(x => x.EmailVerifiedAt).HasColumnName("email_verified_at");
            e.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
            e.Property(x => x.FailedLoginCount).HasColumnName("failed_login_count");
            e.Property(x => x.LockedUntil).HasColumnName("locked_until");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        // BusinessMembership
        modelBuilder.Entity<BusinessMembership>(e =>
        {
            e.ToTable("business_memberships");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.BusinessId).HasColumnName("business_id");
            e.Property(x => x.StaffUserId).HasColumnName("staff_user_id");
            e.Property(x => x.Role).HasColumnName("role");
            e.Property(x => x.DepartmentIds).HasColumnName("department_ids");
            e.Property(x => x.InvitedBy).HasColumnName("invited_by");
            e.Property(x => x.InvitationAcceptedAt).HasColumnName("invitation_accepted_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasOne<Business>()
                .WithMany()
                .HasForeignKey(x => x.BusinessId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne<StaffUser>()
                .WithMany()
                .HasForeignKey(x => x.StaffUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Department
        modelBuilder.Entity<Department>(e =>
        {
            e.ToTable("departments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.BusinessId).HasColumnName("business_id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.Icon).HasColumnName("icon");
            e.Property(x => x.HandlesCategories).HasColumnName("handles_categories");
            e.Property(x => x.SlaMinutes).HasColumnName("sla_minutes");
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.Property(x => x.IsActive).HasColumnName("is_active");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasQueryFilter(x => x.IsActive);
        });
    }
}
