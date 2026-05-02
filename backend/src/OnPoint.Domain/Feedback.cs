namespace OnPoint.Domain;

public class Feedback
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public Guid SessionId { get; set; }
    public Guid? LocationId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? CategoryHint { get; set; }
    public string ClassificationStatus { get; set; } = "pending";
    public string? Sentiment { get; set; }
    public string[]? Categories { get; set; }
    public string? Severity { get; set; }
    public Guid? RoutedToDeptId { get; set; }
    public bool RedirectedToPublic { get; set; } = false;
    public bool ContainsPii { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
