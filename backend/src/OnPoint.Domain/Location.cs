namespace OnPoint.Domain;

public class Location
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public string Name { get; set; } = default!;
    public string? Label { get; set; }
    public string Type { get; set; } = "other";
    public string ShortCode { get; set; } = default!;
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
