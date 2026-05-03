namespace OnPoint.Domain;

public class Business
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = default!;
    public string Name { get; set; } = default!;
    public BusinessType Type { get; set; } = BusinessType.other;
    public BusinessPlan Plan { get; set; } = BusinessPlan.trial;
    public string Timezone { get; set; } = "UTC";
    public string Locale { get; set; } = "en-US";
    public string? LogoUrl { get; set; }
    public Dictionary<string, string> PublicReviewLinks { get; set; } = new();
    public Dictionary<string, object> EarningRules { get; set; } = new();
    public Dictionary<string, object> Settings { get; set; } = new();
    public DateTime? TrialEndsAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
