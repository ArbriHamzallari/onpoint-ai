using System.Text.Json;
using System.Text.Json.Serialization;

namespace OnPoint.Infrastructure.Ai;

// ── Request ────────────────────────────────────────────────────────────────────
// Maps to Python PipelineRunRequest (snake_case). Sent via POST /api/v1/pipeline/run.

internal sealed class AiPipelineRunRequest
{
    [JsonPropertyName("text")]           public string Text { get; init; } = "";
    [JsonPropertyName("business_id")]    public Guid BusinessId { get; init; }
    [JsonPropertyName("session_id")]     public Guid? SessionId { get; init; }
    [JsonPropertyName("issue_id")]       public Guid? IssueId { get; init; }
    [JsonPropertyName("feedback_id")]    public Guid? FeedbackId { get; init; }
    [JsonPropertyName("rating")]         public int? Rating { get; init; }
    [JsonPropertyName("language")]       public string? Language { get; init; }
    [JsonPropertyName("correlation_id")] public string? CorrelationId { get; init; }
}

// ── Per-stage prediction ────────────────────────────────────────────────────────
// Maps to Python PipelineStageResult.

internal sealed class AiStagePredictionDto
{
    [JsonPropertyName("output")]
    public Dictionary<string, JsonElement> Output { get; init; } = new();

    [JsonPropertyName("explanation")]
    public string Explanation { get; init; } = "";

    [JsonPropertyName("confidence")]
    public double? Confidence { get; init; }

    [JsonPropertyName("provider")]
    public string Provider { get; init; } = "";

    [JsonPropertyName("model_version")]
    public string ModelVersion { get; init; } = "";

    [JsonPropertyName("prompt_version")]
    public string? PromptVersion { get; init; }

    [JsonPropertyName("latency_ms")]
    public int LatencyMs { get; init; }

    [JsonPropertyName("cost_usd")]
    public double CostUsd { get; init; }

    [JsonPropertyName("ai_fallback")]
    public bool AiFallback { get; init; }

    [JsonPropertyName("fallback_reason")]
    public string? FallbackReason { get; init; }
}

// ── Full pipeline response ──────────────────────────────────────────────────────
// Maps to Python PipelineRunResponse.

internal sealed class AiPipelineRunResponse
{
    [JsonPropertyName("sentiment")]
    public AiStagePredictionDto Sentiment { get; init; } = new();

    [JsonPropertyName("classifier")]
    public AiStagePredictionDto Classifier { get; init; } = new();

    [JsonPropertyName("priority")]
    public AiStagePredictionDto Priority { get; init; } = new();

    [JsonPropertyName("router")]
    public AiStagePredictionDto Router { get; init; } = new();

    [JsonPropertyName("total_latency_ms")]
    public int TotalLatencyMs { get; init; }

    [JsonPropertyName("total_cost_usd")]
    public double TotalCostUsd { get; init; }
}
