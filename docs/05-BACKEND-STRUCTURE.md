# OnPoint AI — Backend Architecture (ASP.NET Core 8)

## Folder structure (modular monolith)

```
backend/
├── src/
│   ├── OnPoint.Api/                       # ASP.NET Core host
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   ├── Endpoints/                     # Minimal API endpoints, grouped by module
│   │   │   ├── PublicEndpoints.cs
│   │   │   ├── FeedbackEndpoints.cs
│   │   │   ├── IssueEndpoints.cs
│   │   │   ├── RewardEndpoints.cs
│   │   │   └── ...
│   │   ├── Hubs/
│   │   │   ├── StaffHub.cs
│   │   │   └── GuestHub.cs
│   │   ├── Middleware/
│   │   │   ├── TenantResolutionMiddleware.cs
│   │   │   ├── CorrelationIdMiddleware.cs
│   │   │   └── ProblemDetailsMiddleware.cs
│   │   └── Configuration/
│   │       ├── AuthSetup.cs
│   │       ├── DatabaseSetup.cs
│   │       ├── SignalRSetup.cs
│   │       └── HangfireSetup.cs
│   │
│   ├── OnPoint.Domain/                    # Pure domain — no EF, no ASP.NET
│   │   ├── Tenancy/
│   │   │   ├── Business.cs
│   │   │   ├── Department.cs
│   │   │   └── Location.cs
│   │   ├── Identity/
│   │   │   ├── StaffUser.cs
│   │   │   ├── GuestUser.cs
│   │   │   ├── BusinessMembership.cs
│   │   │   └── FeedbackSession.cs
│   │   ├── Feedback/
│   │   │   ├── Feedback.cs
│   │   │   └── FeedbackEnums.cs
│   │   ├── Issues/
│   │   │   ├── Issue.cs
│   │   │   ├── IssueComment.cs
│   │   │   └── IssueEvent.cs
│   │   ├── Rewards/
│   │   │   ├── PointsLedgerEntry.cs
│   │   │   ├── Reward.cs
│   │   │   ├── Redemption.cs
│   │   │   └── EarningRules.cs
│   │   ├── Fraud/
│   │   │   └── FraudSignal.cs
│   │   ├── Notifications/
│   │   │   └── Notification.cs
│   │   └── Common/
│   │       ├── Result.cs
│   │       ├── DomainEvent.cs
│   │       └── Errors.cs
│   │
│   ├── OnPoint.Application/               # Use cases, services, DTOs
│   │   ├── Feedback/
│   │   │   ├── SubmitFeedbackHandler.cs
│   │   │   ├── ClassifyFeedbackJob.cs
│   │   │   └── Dtos/
│   │   ├── Issues/
│   │   │   ├── CreateIssueFromFeedbackHandler.cs
│   │   │   ├── ChangeIssueStatusHandler.cs
│   │   │   └── Dtos/
│   │   ├── Rewards/
│   │   │   ├── AwardPointsHandler.cs
│   │   │   ├── RedeemRewardHandler.cs
│   │   │   ├── EarningRulesEvaluator.cs
│   │   │   └── Dtos/
│   │   ├── Fraud/
│   │   │   ├── FraudScorer.cs
│   │   │   ├── Signals/                  # one file per signal
│   │   │   │   ├── VelocitySignal.cs
│   │   │   │   ├── FingerprintDuplicationSignal.cs
│   │   │   │   ├── IpClusterSignal.cs
│   │   │   │   ├── TextSimilaritySignal.cs
│   │   │   │   └── HoneypotSignal.cs
│   │   │   └── IFraudSignal.cs
│   │   ├── Identity/
│   │   │   ├── StaffAuthService.cs
│   │   │   ├── GuestSessionService.cs
│   │   │   ├── GuestAccountUpgradeHandler.cs
│   │   │   └── JwtIssuer.cs
│   │   ├── AI/
│   │   │   ├── IFeedbackClassifier.cs    # the abstraction
│   │   │   └── ClassificationContext.cs
│   │   ├── Notifications/
│   │   │   ├── INotifier.cs
│   │   │   ├── IssueNotifier.cs
│   │   │   └── PointsNotifier.cs
│   │   ├── Tenancy/
│   │   │   └── ITenantContext.cs
│   │   ├── Validation/                    # FluentValidation validators
│   │   └── Behaviors/                     # MediatR pipeline behaviors
│   │
│   ├── OnPoint.Infrastructure/            # EF Core, external services
│   │   ├── Persistence/
│   │   │   ├── OnPointDbContext.cs
│   │   │   ├── Migrations/
│   │   │   └── Configurations/           # IEntityTypeConfiguration<T>
│   │   ├── Tenancy/
│   │   │   └── HttpTenantContext.cs      # reads from HttpContext
│   │   ├── Identity/
│   │   │   ├── PasswordHasher.cs
│   │   │   └── SessionTokenSigner.cs
│   │   ├── AI/
│   │   │   ├── AzureOpenAIClassifier.cs
│   │   │   ├── OpenAIClassifier.cs
│   │   │   └── StubClassifier.cs
│   │   ├── Caching/
│   │   │   └── RedisCache.cs
│   │   ├── Notifications/
│   │   │   ├── EmailSender.cs            # Azure Communication Services
│   │   │   └── SmsSender.cs
│   │   ├── Storage/
│   │   │   └── AzureBlobStorage.cs
│   │   └── Jobs/                          # Hangfire job definitions
│   │       ├── ExpirePointsJob.cs
│   │       ├── IssueAutoEscalateJob.cs
│   │       └── MetricsRollupJob.cs
│   │
│   └── OnPoint.Shared/                    # cross-cutting (logging, errors)
│       └── ProblemDetails/
│
├── tests/
│   ├── OnPoint.UnitTests/                 # domain + application unit tests
│   ├── OnPoint.IntegrationTests/          # full stack with Testcontainers
│   └── OnPoint.ArchitectureTests/         # NetArchTest — enforce module rules
│
├── docker-compose.yml                     # local dev: postgres + redis + mailhog
├── OnPoint.sln
├── Directory.Packages.props               # central package versioning
├── Directory.Build.props                  # shared csproj settings
├── .editorconfig
└── README.md
```

## Key dependencies

```xml
<!-- Directory.Packages.props -->
<Project>
  <ItemGroup>
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.*" />
    <PackageVersion Include="Microsoft.AspNetCore.SignalR.StackExchangeRedis" Version="8.0.*" />
    <PackageVersion Include="Microsoft.Azure.SignalR" Version="1.27.*" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="8.0.*" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.*" />
    <PackageVersion Include="Hangfire.AspNetCore" Version="1.8.*" />
    <PackageVersion Include="Hangfire.PostgreSql" Version="1.20.*" />
    <PackageVersion Include="StackExchange.Redis" Version="2.8.*" />
    <PackageVersion Include="MediatR" Version="12.*" />
    <PackageVersion Include="FluentValidation.AspNetCore" Version="11.*" />
    <PackageVersion Include="Serilog.AspNetCore" Version="8.0.*" />
    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.9.*" />
    <PackageVersion Include="Azure.Identity" Version="1.13.*" />
    <PackageVersion Include="Azure.Security.KeyVault.Secrets" Version="4.6.*" />
    <PackageVersion Include="Azure.Storage.Blobs" Version="12.22.*" />
    <PackageVersion Include="Azure.AI.OpenAI" Version="2.*" />
    <PackageVersion Include="AspNetCoreRateLimit" Version="5.0.*" />
    <PackageVersion Include="QRCoder" Version="1.6.*" />
    <PackageVersion Include="BCrypt.Net-Next" Version="4.0.*" />
    <PackageVersion Include="Otp.NET" Version="1.4.*" />
    <PackageVersion Include="Mapster" Version="7.4.*" />
  </ItemGroup>
</Project>
```

## Architectural rules (enforced by ArchitectureTests)

- `Domain` references nothing.
- `Application` references `Domain` only.
- `Infrastructure` references `Domain` and `Application`.
- `Api` references all three.
- No module-to-module direct calls inside `Application` — go through MediatR.
- No `DbContext` outside `Infrastructure`.
- No `HttpContext` outside `Api` and `Infrastructure/Tenancy`.

## Tenant resolution flow

```csharp
// Middleware/TenantResolutionMiddleware.cs (in Api)
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx, ITenantContext tenant, OnPointDbContext db)
    {
        // Path 1: Staff JWT
        var bizClaim = ctx.User?.FindFirst("business_id")?.Value;
        if (Guid.TryParse(bizClaim, out var bizId))
        {
            tenant.SetBusiness(bizId);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SET LOCAL app.current_business_id = {bizId}");
        }
        // Path 2: Guest session cookie → resolve session → business_id
        else if (ctx.Request.Cookies.TryGetValue("op_session", out var token))
        {
            var session = await GuestSessionService.Resolve(token, db);
            if (session is not null)
            {
                tenant.SetBusiness(session.BusinessId);
                tenant.SetSession(session.Id);
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"SET LOCAL app.current_business_id = {session.BusinessId}");
            }
        }
        // Path 3: Platform admin
        else if (ctx.User?.IsInRole("platform_admin") == true)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SET LOCAL app.is_platform_admin = 'true'");
        }

        await _next(ctx);
    }
}
```

## Key services to scaffold first (Cursor priority order)

1. `OnPointDbContext` + entity configurations + first migration
2. `ITenantContext` + middleware
3. `IFeedbackClassifier` + `StubClassifier` (real one comes later)
4. `IFraudSignal` + scorer + 3 simplest signals (velocity, honeypot, fingerprint)
5. `EarningRulesEvaluator`
6. `SubmitFeedbackHandler`
7. `RedeemRewardHandler` (transactional, with `SERIALIZABLE` isolation)
8. `StaffHub` + `GuestHub` + `IssueNotifier`
9. JWT issuer + auth endpoints
10. Public endpoints (`/r/{shortCode}`, `/sessions`, `/feedback`)
