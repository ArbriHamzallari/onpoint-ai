namespace OnPoint.Domain;

public class BusinessMembership
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public Guid StaffUserId { get; set; }
    public UserRole Role { get; set; }
    public Guid[]? DepartmentIds { get; set; }
    public Guid? InvitedBy { get; set; }
    public DateTime? InvitationAcceptedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
