using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OnPoint.API.Middleware;
using OnPoint.Application.Ai;
using OnPoint.Application.Tenancy;
using OnPoint.Domain;
using OnPoint.Infrastructure.Ai;
using OnPoint.Infrastructure.Auth;
using OnPoint.Infrastructure.Identity;
using OnPoint.Infrastructure.Persistence;
using OnPoint.Infrastructure.Feedback;
using OnPoint.Infrastructure.Issues;
using OnPoint.Infrastructure.Departments;
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
            .MapEnum<PointsEntryStatus>("points_entry_status")
            .MapEnum<AiStage>("ai_stage")
            .MapEnum<AiProvider>("ai_provider")));

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
builder.Services.AddScoped<DepartmentHandler>();

// ── AI Pipeline ───────────────────────────────────────────────────────────────
builder.Services.Configure<AiClientOptions>(
    builder.Configuration.GetSection("AiService"));
builder.Services.AddHttpClient<IAiService, AiClient>();
// AiPipelineQueue is a singleton — one channel shared across all requests.
builder.Services.AddSingleton<AiPipelineQueue>();
builder.Services.AddSingleton<IAiPipelineQueue>(sp =>
    sp.GetRequiredService<AiPipelineQueue>());
builder.Services.AddScoped<AiPipelineOrchestrator>();
builder.Services.AddHostedService<AiPipelineBackgroundService>();

// ── HTTP ──────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

// Development-only: demo seed data
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHostedService<OnPoint.Infrastructure.Seeding.DemoSeedService>();
}

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();

// Must come AFTER UseAuthentication so JWT claims are available
app.UseMiddleware<TenantResolutionMiddleware>();

app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
