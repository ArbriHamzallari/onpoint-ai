using OnPoint.Application.Tenancy;

namespace OnPoint.Infrastructure.Tenancy;

public class HttpTenantContext : ITenantContext
{
    private Guid? _businessId;
    private Guid? _sessionId;
    private bool _isPlatformAdmin;

    public Guid BusinessId => _businessId
        ?? throw new InvalidOperationException(
            "BusinessId not set. Ensure TenantResolutionMiddleware has run.");

    public Guid? SessionId => _sessionId;
    public bool IsAuthenticated => _businessId.HasValue;
    public bool IsPlatformAdmin => _isPlatformAdmin;

    public void SetBusiness(Guid businessId) => _businessId = businessId;
    public void SetSession(Guid sessionId) => _sessionId = sessionId;
    public void SetPlatformAdmin() => _isPlatformAdmin = true;
}
