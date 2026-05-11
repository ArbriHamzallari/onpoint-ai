using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using OnPoint.Application.Ai;
using OnPoint.Application.Events;
using OnPoint.Domain;
using OnPoint.Infrastructure.Persistence;

namespace OnPoint.Infrastructure.Ai;

/// <summary>
/// Runs the AI pipeline for one issue and persists all results atomically:
///   1. Calls IAiService — if null (service down), marks issue ai_fallback=true and returns.
///   2. Writes 4 AiPrediction rows (one per stage).
///   3. Updates Issue: AiCategory, AiCategoryConfidence, AiPriorityScore, AiFallback.
///   4. If classifier confidence ≥ 0.70 → sets AiCategory.
///   5. If priority confidence   ≥ 0.70 → promotes Issue.Priority enum.
///   6. If router confidence     ≥ 0.70 and issue still open → sets DepartmentId,
///      advances status to assigned.
///
/// On relational providers (Npgsql): wraps writes in an explicit transaction so
/// SET LOCAL RLS context is scoped. On InMemory (unit tests): SaveChanges is atomic
/// itself — the transaction is skipped to avoid an unsupported-operation exception.
/// </summary>
public sealed class AiPipelineOrchestrator
{
    private const double ConfidenceThreshold = 0.70;

    private readonly IAiService _ai;
    private readonly AppDbContext _db;
    private readonly IIssueEventPublisher _events;
    private readonly IGuestStatusPublisher _guestEvents;
    private readonly ILogger<AiPipelineOrchestrator> _logger;

    public AiPipelineOrchestrator(
        IAiService ai,
        AppDbContext db,
        IIssueEventPublisher events,
        IGuestStatusPublisher guestEvents,
        ILogger<AiPipelineOrchestrator> logger)
    {
        _ai          = ai;
        _db          = db;
        _events      = events;
        _guestEvents = guestEvents;
        _logger      = logger;
    }

    public async Task ProcessAsync(AiPipelineRequest request, CancellationToken ct = default)
    {
        var issue = await _db.Issues
            .FirstOrDefaultAsync(i =>
                i.Id == request.IssueId && i.BusinessId == request.BusinessId, ct);

        if (issue is null)
        {
            _logger.LogWarning(
                "AI pipeline: issue {IssueId} not found for business {BusinessId}, skipping.",
                request.IssueId, request.BusinessId);
            return;
        }

        var result = await _ai.RunPipelineAsync(request, ct);
        var now    = DateTime.UtcNow;

        if (result is null)
        {
            // AI service unreachable — mark issue and exit without prediction rows.
            // CLAUDE.md §AI Engineering #5: never silent about fallback.
            issue.AiFallback = true;
            issue.UpdatedAt  = now;
            await _db.SaveChangesAsync(ct);

            _logger.LogWarning(
                "AI pipeline: service unavailable for issue {IssueId}. " +
                "Marked ai_fallback=true; no prediction rows stored.",
                request.IssueId);

            // Notify dashboard so the ai_fallback badge appears on the issue card.
            await _events.IssueUpdatedAsync(request.BusinessId, issue.Id, ct);
            return;
        }

        // Relational (Npgsql): explicit transaction so SET LOCAL RLS context is scoped
        // to the writes below. InMemory (unit tests): transactions are not supported —
        // SaveChanges is atomic by itself, skip BEGIN/COMMIT entirely.
        IDbContextTransaction? tx = null;
        try
        {
            if (_db.Database.IsRelational())
            {
                tx = await _db.Database.BeginTransactionAsync(ct);

                // Postgres SET is a utility statement and does NOT accept bound
                // parameters. ExecuteSqlAsync with $"..." would emit `SET ... = $1`
                // which Postgres rejects. Use ExecuteSqlRawAsync with the Guid
                // embedded literally — safe because Guid is value-validated and
                // cannot contain quote characters.
#pragma warning disable EF1003 // SET cannot be parameterized; Guid is safe to interpolate
                await _db.Database.ExecuteSqlRawAsync(
                    "SET LOCAL app.current_business_id = '" + request.BusinessId + "'", ct);
#pragma warning restore EF1003
            }

            // ── Persist predictions ──────────────────────────────────────────
            StorePrediction(request, AiStage.sentiment,  result.Sentiment,  now);
            StorePrediction(request, AiStage.classifier, result.Classifier, now);
            StorePrediction(request, AiStage.priority,   result.Priority,   now);
            StorePrediction(request, AiStage.router,     result.Router,     now);

            // ── Update issue ─────────────────────────────────────────────────

            // Category — only apply when confidence meets the CLAUDE.md threshold (0.70).
            if (result.Category is not null
                && result.ClassifierConfidence >= ConfidenceThreshold)
            {
                issue.AiCategory           = result.Category;
                issue.AiCategoryConfidence = (decimal?)Math.Round(
                    result.ClassifierConfidence.Value, 4);
            }

            // Priority score — always store the raw 0-100 number for display.
            issue.AiPriorityScore = result.PriorityScore;

            // Promote the priority enum when AI is confident.
            if (result.PriorityLabel is not null
                && result.PriorityConfidence >= ConfidenceThreshold
                && Enum.TryParse<IssuePriority>(
                    result.PriorityLabel, ignoreCase: true, out var aiPriority))
            {
                issue.Priority = aiPriority;
            }

            // Department routing — only auto-route when:
            //   • Router confidence ≥ 0.70 (CLAUDE.md §AI Engineering #6)
            //   • Issue is still open (no human has touched it yet)
            bool departmentChanged = false;
            if (result.DepartmentKey is not null
                && result.RouterConfidence >= ConfidenceThreshold
                && issue.Status == IssueStatus.open)
            {
                var dept = await _db.Departments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d =>
                        d.BusinessId == request.BusinessId &&
                        d.HandlesCategories.Contains(result.DepartmentKey), ct);

                if (dept is not null)
                {
                    issue.DepartmentId = dept.Id;
                    issue.Status       = IssueStatus.assigned;
                    departmentChanged  = true;
                }
            }

            issue.AiFallback = result.AnyFallback;
            issue.UpdatedAt  = now;

            await _db.SaveChangesAsync(ct);

            if (tx is not null)
                await tx.CommitAsync(ct);

            // Notify subscribed dashboards AFTER commit so the refetch sees the new
            // row. IssueAssigned when AI auto-routed (status moved open → assigned),
            // otherwise IssueUpdated (just AI fields refreshed).
            if (departmentChanged)
                await _events.IssueAssignedAsync(request.BusinessId, issue.Id, ct);
            else
                await _events.IssueUpdatedAsync(request.BusinessId, issue.Id, ct);

            await _events.DashboardStatsChangedAsync(request.BusinessId, ct);

            // Notify the guest's status screen too. AiUpdateAdded surfaces the
            // category/dept on their timeline. If AI auto-routed, also fire
            // StatusChanged so the timeline advances (open → assigned).
            await _guestEvents.AiUpdateAddedAsync(request.SessionId, issue.Id, ct);
            if (departmentChanged)
            {
                await _guestEvents.StatusChangedAsync(
                    request.SessionId, issue.Id, issue.Status.ToString(), ct);
            }

            _logger.LogInformation(
                "AI pipeline complete — issue {IssueId}: " +
                "category={Category} ({CatConf:P0}), " +
                "priorityScore={PScore}, priority={Priority}, " +
                "dept={DeptKey} ({RouterConf:P0}), " +
                "totalMs={Ms}, cost=${Cost:F5}, fallback={Fallback}.",
                request.IssueId,
                result.Category,        result.ClassifierConfidence,
                result.PriorityScore,   issue.Priority,
                result.DepartmentKey,   result.RouterConfidence,
                result.TotalLatencyMs,  result.TotalCostUsd,
                result.AnyFallback);
        }
        catch
        {
            if (tx is not null)
                await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (tx is not null)
                await tx.DisposeAsync();
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void StorePrediction(
        AiPipelineRequest request,
        AiStage stage,
        AiStageData data,
        DateTime now)
    {
        if (!Enum.TryParse<AiProvider>(data.Provider, ignoreCase: true, out var provider))
            provider = AiProvider.rule_based;

        _db.AiPredictions.Add(new AiPrediction
        {
            Id             = Guid.NewGuid(),
            BusinessId     = request.BusinessId,
            IssueId        = request.IssueId,
            FeedbackId     = request.FeedbackId,
            SessionId      = request.SessionId,
            Stage          = stage,
            InputHash      = HashText(request.Text),
            OutputJson     = data.OutputJson,
            Explanation    = data.Explanation,
            ContainsPii    = false, // guest text stripped before training corpus per CLAUDE.md §AI #7
            ModelVersion   = data.ModelVersion,
            PromptVersion  = data.PromptVersion,
            Provider       = provider,
            Confidence     = data.Confidence.HasValue
                                 ? (decimal)Math.Round(data.Confidence.Value, 4)
                                 : null,
            LatencyMs      = data.LatencyMs,
            CostUsd        = (decimal)data.CostUsd,
            AiFallback     = data.AiFallback,
            FallbackReason = data.FallbackReason,
            CreatedAt      = now,
        });
    }

    private static string HashText(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
