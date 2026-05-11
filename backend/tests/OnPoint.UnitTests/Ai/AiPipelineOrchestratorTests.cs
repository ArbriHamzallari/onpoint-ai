using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OnPoint.Application.Ai;
using OnPoint.Domain;
using OnPoint.Infrastructure.Ai;
using OnPoint.Infrastructure.Persistence;
using Xunit;

namespace OnPoint.UnitTests.Ai;

public class AiPipelineOrchestratorTests
{
    // ── DB factory ─────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    // ── Test data builders ─────────────────────────────────────────────────────

    private static AiStageData MakeStage(
        string outputJson,
        double confidence,
        bool fallback = false) =>
        new(
            OutputJson:     outputJson,
            Explanation:    "test",
            Confidence:     confidence,
            Provider:       fallback ? "rule_based" : "openai",
            ModelVersion:   "test@1.0.0",
            PromptVersion:  null,
            LatencyMs:      10,
            CostUsd:        0.001,
            AiFallback:     fallback,
            FallbackReason: fallback ? "test fallback" : null
        );

    // High-confidence result that should trigger every update path.
    private static AiPipelineResult HighConfidenceResult() => new(
        Sentiment:            MakeStage(@"{""sentiment"":""negative"",""urgency"":0.8}", 0.88),
        Classifier:           MakeStage(@"{""category"":""hvac""}", 0.91),
        Priority:             MakeStage(@"{""priority_score"":75,""priority_label"":""high""}", 0.85),
        Router:               MakeStage(@"{""department_key"":""maintenance"",""alternatives"":[]}", 0.92),
        SentimentLabel:       "negative",
        UrgencyScore:         0.8,
        Category:             "hvac",
        ClassifierConfidence: 0.91,
        PriorityScore:        75,
        PriorityLabel:        "high",
        PriorityConfidence:   0.85,
        DepartmentKey:        "maintenance",
        RouterConfidence:     0.92,
        AnyFallback:          false,
        TotalLatencyMs:       120,
        TotalCostUsd:         0.004
    );

    private static (Issue Issue, Department Dept, AiPipelineRequest Request) Seed(AppDbContext db)
    {
        var businessId = Guid.NewGuid();

        var dept = new Department
        {
            Id                = Guid.NewGuid(),
            BusinessId        = businessId,
            Name              = "Maintenance",
            HandlesCategories = ["maintenance", "hvac", "plumbing"],
            IsActive          = true,
            CreatedAt         = DateTime.UtcNow,
            UpdatedAt         = DateTime.UtcNow,
        };
        db.Departments.Add(dept);

        var issue = new Issue
        {
            Id         = Guid.NewGuid(),
            BusinessId = businessId,
            FeedbackId = Guid.NewGuid(),
            SessionId  = Guid.NewGuid(),
            Title      = "Test issue",
            Status     = IssueStatus.open,
            Priority   = IssuePriority.medium,
            CreatedAt  = DateTime.UtcNow,
            UpdatedAt  = DateTime.UtcNow,
        };
        db.Issues.Add(issue);
        db.SaveChanges();

        var req = new AiPipelineRequest(
            BusinessId:  businessId,
            SessionId:   issue.SessionId,
            IssueId:     issue.Id,
            FeedbackId:  issue.FeedbackId,
            Text:        "AC is broken and the room is freezing",
            Rating:      1
        );

        return (issue, dept, req);
    }

    // ── Fake IAiService ────────────────────────────────────────────────────────

    private sealed class FakeAi : IAiService
    {
        private readonly AiPipelineResult? _result;
        public FakeAi(AiPipelineResult? result) => _result = result;
        public Task<AiPipelineResult?> RunPipelineAsync(
            AiPipelineRequest r, CancellationToken ct = default) =>
            Task.FromResult(_result);
    }

    private static AiPipelineOrchestrator MakeOrchestrator(
        AppDbContext db, AiPipelineResult? result) =>
        new(
            new FakeAi(result),
            db,
            NullLogger<AiPipelineOrchestrator>.Instance
        );

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HappyPath_StoresFourPredictionsAndUpdatesIssue()
    {
        using var db = CreateDb();
        var (_, dept, req) = Seed(db);

        await MakeOrchestrator(db, HighConfidenceResult()).ProcessAsync(req);

        // 4 prediction rows stored
        db.AiPredictions.Should().HaveCount(4);
        db.AiPredictions.Select(p => p.Stage).Should().BeEquivalentTo(
            new[] { AiStage.sentiment, AiStage.classifier, AiStage.priority, AiStage.router },
            opts => opts.WithoutStrictOrdering());

        // Issue updated with AI fields
        var issue = db.Issues.First();
        issue.AiCategory.Should().Be("hvac");
        issue.AiCategoryConfidence.Should().BeApproximately(0.91m, 0.0001m);
        issue.AiPriorityScore.Should().Be(75);
        issue.AiFallback.Should().BeFalse();

        // Priority promoted by AI
        issue.Priority.Should().Be(IssuePriority.high);

        // Auto-routed to Maintenance department
        issue.DepartmentId.Should().Be(dept.Id);
        issue.Status.Should().Be(IssueStatus.assigned);
    }

    [Fact]
    public async Task LowClassifierConfidence_DoesNotSetAiCategory()
    {
        using var db = CreateDb();
        var (_, _, req) = Seed(db);

        // Below the 0.70 threshold
        var result = HighConfidenceResult() with { ClassifierConfidence = 0.55 };

        await MakeOrchestrator(db, result).ProcessAsync(req);

        db.Issues.First().AiCategory.Should().BeNull(
            "classifier confidence 0.55 is below the 0.70 threshold");
    }

    [Fact]
    public async Task LowRouterConfidence_DoesNotAutoAssignDepartment()
    {
        using var db = CreateDb();
        var (_, _, req) = Seed(db);

        var result = HighConfidenceResult() with { RouterConfidence = 0.60 };

        await MakeOrchestrator(db, result).ProcessAsync(req);

        var issue = db.Issues.First();
        issue.DepartmentId.Should().BeNull(
            "router confidence 0.60 is below the 0.70 threshold");
        issue.Status.Should().Be(IssueStatus.open,
            "not auto-assigned when router confidence is too low");
    }

    [Fact]
    public async Task LowPriorityConfidence_DoesNotPromotePriorityEnum()
    {
        using var db = CreateDb();
        var (_, _, req) = Seed(db);

        var result = HighConfidenceResult() with { PriorityConfidence = 0.45 };

        await MakeOrchestrator(db, result).ProcessAsync(req);

        // Priority score stored but enum not promoted
        db.Issues.First().AiPriorityScore.Should().Be(75);
        db.Issues.First().Priority.Should().Be(IssuePriority.medium,
            "original medium priority kept when AI confidence is low");
    }

    [Fact]
    public async Task ServiceUnavailable_MarksAiFallbackAndStoresNoPredictions()
    {
        using var db = CreateDb();
        var (_, _, req) = Seed(db);

        // null = AI service unreachable
        await MakeOrchestrator(db, null).ProcessAsync(req);

        db.AiPredictions.Should().BeEmpty(
            "no service response means no prediction rows");
        db.Issues.First().AiFallback.Should().BeTrue();
    }

    [Fact]
    public async Task AllStageFallbacks_SetsAnyFallbackAndStoresPredictions()
    {
        using var db = CreateDb();
        var (_, _, req) = Seed(db);

        var fallbackResult = HighConfidenceResult() with
        {
            Sentiment  = MakeStage(@"{""sentiment"":""negative"",""urgency"":0.6}", 0.6, fallback: true),
            Classifier = MakeStage(@"{""category"":""other""}", 0.4, fallback: true),
            Priority   = MakeStage(@"{""priority_score"":20,""priority_label"":""low""}", 0.6, fallback: true),
            Router     = MakeStage(@"{""department_key"":""other"",""alternatives"":[]}", 0.4, fallback: true),
            AnyFallback = true,
        };

        await MakeOrchestrator(db, fallbackResult).ProcessAsync(req);

        // Predictions still stored even on fallback (CLAUDE.md §AI #2)
        db.AiPredictions.Should().HaveCount(4);
        db.AiPredictions.All(p => p.AiFallback).Should().BeTrue();
        db.Issues.First().AiFallback.Should().BeTrue();
    }

    [Fact]
    public async Task AlreadyAssignedIssue_SkipsDepartmentAutoRouting()
    {
        using var db = CreateDb();
        var businessId = Guid.NewGuid();

        var dept1 = new Department
        {
            Id = Guid.NewGuid(), BusinessId = businessId, Name = "Front Desk",
            HandlesCategories = ["front_desk", "other"], IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        var dept2 = new Department
        {
            Id = Guid.NewGuid(), BusinessId = businessId, Name = "Maintenance",
            HandlesCategories = ["maintenance", "hvac"], IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        db.Departments.AddRange(dept1, dept2);

        // Issue already assigned to Front Desk by a human
        var issue = new Issue
        {
            Id = Guid.NewGuid(), BusinessId = businessId,
            FeedbackId = Guid.NewGuid(), SessionId = Guid.NewGuid(),
            Title = "Already assigned", Status = IssueStatus.assigned,
            DepartmentId = dept1.Id,    // already set
            Priority = IssuePriority.medium,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        db.Issues.Add(issue);
        db.SaveChanges();

        var req = new AiPipelineRequest(
            BusinessId: businessId, SessionId: issue.SessionId,
            IssueId: issue.Id, FeedbackId: issue.FeedbackId,
            Text: "AC broken", Rating: 1);

        // AI wants to route to Maintenance with high confidence
        var result = HighConfidenceResult() with
        {
            DepartmentKey    = "maintenance",
            RouterConfidence = 0.95,
        };

        await MakeOrchestrator(db, result).ProcessAsync(req);

        // Should NOT override human assignment because status != open
        db.Issues.First().DepartmentId.Should().Be(dept1.Id,
            "human assignment should not be overridden by AI on non-open issues");
    }
}
