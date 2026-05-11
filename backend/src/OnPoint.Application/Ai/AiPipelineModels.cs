namespace OnPoint.Application.Ai;

// Context passed to the AI pipeline background worker for one issue.
// businessId always comes from ITenantContext — never extracted inside handlers.
public record AiPipelineRequest(
    Guid BusinessId,
    Guid SessionId,
    Guid IssueId,
    Guid FeedbackId,
    string Text,
    int? Rating,
    string? CorrelationId = null
);

// Per-stage raw data — kept in Application so tests can reference it
// without pulling in Infrastructure's System.Text.Json DTOs.
public record AiStageData(
    string OutputJson,        // raw JSON string for ai_predictions.output_json
    string Explanation,
    double? Confidence,
    string Provider,
    string ModelVersion,
    string? PromptVersion,
    int LatencyMs,
    double CostUsd,
    bool AiFallback,
    string? FallbackReason
);

// Full 4-stage pipeline result returned by IAiService.
// A null return from IAiService means the service is unreachable — the orchestrator
// must then set AiFallback=true on the issue (CLAUDE.md §AI Engineering #5).
public record AiPipelineResult(
    AiStageData Sentiment,
    AiStageData Classifier,
    AiStageData Priority,
    AiStageData Router,
    // Extracted typed values for quick use — null when below confidence or not returned
    string? SentimentLabel,
    double? UrgencyScore,
    string? Category,
    double? ClassifierConfidence,
    int? PriorityScore,
    string? PriorityLabel,
    double? PriorityConfidence,
    string? DepartmentKey,
    double? RouterConfidence,
    bool AnyFallback,
    int TotalLatencyMs,
    double TotalCostUsd
);
