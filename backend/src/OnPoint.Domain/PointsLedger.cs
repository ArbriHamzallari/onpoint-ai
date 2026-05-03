namespace OnPoint.Domain;

public class PointsLedger
{
    public Guid Id { get; set; }
    public Guid? GuestUserId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid BusinessId { get; set; }
    public int Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? FeedbackId { get; set; }
    public Guid? IssueId { get; set; }
    public Guid? RedemptionId { get; set; }
    public PointsEntryStatus Status { get; set; } = PointsEntryStatus.confirmed;
    public int FraudScore { get; set; }
    public bool Flagged { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Guid? ReversedByEntryId { get; set; }
    public string? ReversedReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}
