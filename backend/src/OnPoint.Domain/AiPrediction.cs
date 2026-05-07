namespace OnPoint.Domain;

public class AiPrediction
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public Guid? IssueId { get; set; }
    public Guid? FeedbackId { get; set; }
    public Guid? SessionId { get; set; }
    public AiStage Stage { get; set; }
    public string InputHash { get; set; } = default!;
    public string OutputJson { get; set; } = "{}";
    public string? Explanation { get; set; }
    public string? PromptText { get; set; }
    public string? ResponseText { get; set; }
    public bool ContainsPii { get; set; } = true;
    public Guid? ModelVersionId { get; set; }
    public string ModelVersion { get; set; } = default!;
    public string? PromptVersion { get; set; }
    public AiProvider Provider { get; set; }
    public decimal? Confidence { get; set; }
    public int LatencyMs { get; set; }
    public decimal? CostUsd { get; set; }
    public bool AiFallback { get; set; }
    public string? FallbackReason { get; set; }
    public DateTime CreatedAt { get; set; }
}
