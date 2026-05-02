namespace OnPoint.Domain;

public class FeedbackSession
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? GuestUserId { get; set; }
    public string? DeviceFingerprintHash { get; set; }
    public string? IpHash { get; set; }
    public string? UserAgent { get; set; }
    public string? GeoCountry { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime LastActiveAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int FraudScore { get; set; } = 0;
}
