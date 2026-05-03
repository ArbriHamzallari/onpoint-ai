namespace OnPoint.Domain;

public class GuestUser
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? FullName { get; set; }
    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }
    public string? Locale { get; set; }
    public int FraudScore { get; set; }
    public bool IsBlocked { get; set; }
    public string? BlockedReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime? AnonymizedAt { get; set; }
}
