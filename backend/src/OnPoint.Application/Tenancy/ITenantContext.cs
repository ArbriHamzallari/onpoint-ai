namespace OnPoint.Application.Tenancy;

public interface ITenantContext
{
    Guid BusinessId { get; }
    Guid? SessionId { get; }
    bool IsAuthenticated { get; }
    bool IsPlatformAdmin { get; }

    void SetBusiness(Guid businessId);
    void SetSession(Guid sessionId);
    void SetPlatformAdmin();
}
