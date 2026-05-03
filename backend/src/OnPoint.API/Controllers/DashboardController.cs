using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnPoint.Application.Tenancy;
using OnPoint.Infrastructure.Issues;

namespace OnPoint.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IssueHandler _handler;
    private readonly ITenantContext _tenant;

    public DashboardController(IssueHandler handler, ITenantContext tenant)
    {
        _handler = handler;
        _tenant = tenant;
    }

    // GET /api/dashboard/stats
    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        var result = await _handler.GetStatsAsync(_tenant.BusinessId, ct);
        return Ok(result);
    }
}
