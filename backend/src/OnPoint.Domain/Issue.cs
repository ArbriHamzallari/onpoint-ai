namespace OnPoint.Domain;

public class Issue
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public Guid FeedbackId { get; set; }
    public Guid SessionId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? AssignedTo { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public string Status { get; set; } = "open";
    public string Priority { get; set; } = "medium";
    public string? ResolutionNote { get; set; }
    public Guid? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public bool? GuestConfirmedResolution { get; set; }
    public DateTime? SlaBreachAt { get; set; }
    public bool SlaBreached { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
