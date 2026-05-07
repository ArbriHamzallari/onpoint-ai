namespace OnPoint.Domain;

public class ModelVersion
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Version { get; set; } = default!;
    public AiProvider Provider { get; set; }
    public string? ModelId { get; set; }
    public string? PromptVersion { get; set; }
    public DateTime DeployedAt { get; set; }
    public DateTime? ShadowUntil { get; set; }
    public int CanaryPercent { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal? CostPer1kInputTokens { get; set; }
    public decimal? CostPer1kOutputTokens { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
