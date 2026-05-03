using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnPoint.Application.Tenancy;
using OnPoint.Infrastructure.Issues;

namespace OnPoint.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class IssuesController : ControllerBase
{
    private readonly IssueHandler _handler;
    private readonly ITenantContext _tenant;

    public IssuesController(IssueHandler handler, ITenantContext tenant)
    {
        _handler = handler;
        _tenant = tenant;
    }

    // GET /api/issues?status=open&departmentId=...&locationId=...&page=1&pageSize=20
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] IssueListRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _handler.ListAsync(_tenant.BusinessId, request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET /api/issues/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var result = await _handler.GetDetailAsync(_tenant.BusinessId, id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    // POST /api/issues/{id}/start
    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _handler.StartAsync(_tenant.BusinessId, id, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    // POST /api/issues/{id}/resolve
    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _handler.ResolveAsync(_tenant.BusinessId, id, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    // PATCH /api/issues/{id}/assign
    [HttpPatch("{id:guid}/assign")]
    public async Task<IActionResult> Assign(
        Guid id,
        [FromBody] AssignIssueRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _handler.AssignAsync(
                _tenant.BusinessId, id, request.DepartmentId, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}
