using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OnPoint.API.Middleware;
using OnPoint.Application.Tenancy;
using OnPoint.Domain;
using OnPoint.Infrastructure.Auth;
using OnPoint.Infrastructure.Identity;
using OnPoint.Infrastructure.Persistence;
using OnPoint.Infrastructure.Feedback;
using OnPoint.Infrastructure.Issues;
using OnPoint.Infrastructure.Locations;
using OnPoint.Infrastructure.Sessions;
using OnPoint.Infrastructure.Tenancy;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ───────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration));

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => npgsqlOptions
            .MapEnum<BusinessType>("business_type")
            .MapEnum<BusinessPlan>("business_plan")
            .MapEnum<LocationType>("location_type")
            .MapEnum<UserRole>("user_role")
            .MapEnum<FeedbackSentiment>("feedback_sentiment")
            .MapEnum<FeedbackSeverity>("feedback_severity")
            .MapEnum<IssueStatus>("issue_status")
            .MapEnum<IssuePriority>("issue_priority")
            .MapEnum<PointsEntryStatus>("points_entry_status")));

// ── JWT Auth ──────────────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret not configured");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer   = true,
            ValidIssuer      = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience    = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew        = TimeSpan.FromSeconds(30)
        };

        // Allow token from query string — needed for SignalR in Phase 5
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddScoped<JwtIssuer>();
builder.Services.AddScoped<StaffAuthHandler>();
builder.Services.AddScoped<GuestSessionHandler>();
builder.Services.AddScoped<FraudScorer>();
builder.Services.AddScoped<PointsService>();
builder.Services.AddScoped<FeedbackHandler>();
builder.Services.AddScoped<IssueHandler>();
builder.Services.AddScoped<LocationHandler>();

// ── HTTP ──────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();
app.UseAuthentication();

// Must come AFTER UseAuthentication so JWT claims are available
app.UseMiddleware<TenantResolutionMiddleware>();

app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// ── Dev seed ──────────────────────────────────────────────────────────────────
// Inserts a test business + location so QR endpoints can be tested immediately.
// Remove this block before going to production.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (!await db.Businesses.AnyAsync())
    {
        var bizId = Guid.NewGuid();

        db.Businesses.Add(new OnPoint.Domain.Business
        {
            Id        = bizId,
            Slug      = "oceanview",
            Name      = "Oceanview Hotel",
            Type      = BusinessType.hotel,
            Plan      = BusinessPlan.trial,
            Timezone  = "Europe/Tirane",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        db.Locations.Add(new OnPoint.Domain.Location
        {
            Id         = Guid.NewGuid(),
            BusinessId = bizId,
            Name       = "Room 204",
            Label      = "Deluxe",
            Type       = LocationType.room,
            ShortCode  = "test-qr-001",
            IsActive   = true,
            CreatedAt  = DateTime.UtcNow,
            UpdatedAt  = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }
}

app.Run();
