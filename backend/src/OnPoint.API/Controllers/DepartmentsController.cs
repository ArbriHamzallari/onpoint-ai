using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnPoint.Application.Tenancy;
using OnPoint.Infrastructure.Departments;

namespace OnPoint.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DepartmentsController : ControllerBase
{
    private readonly DepartmentHandler _handler;
    private readonly ITenantContext _tenant;

    public DepartmentsController(
        DepartmentHandler handler,
        ITenantContext tenant)
    {
        _handler = handler;
        _tenant = tenant;
    }

    // GET /api/departments
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _handler.ListAsync(_tenant.BusinessId, ct);
        return Ok(result);
    }

    // GET /api/departments/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var result = await _handler.GetDetailAsync(
            _tenant.BusinessId, id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    // POST /api/departments
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateDepartmentRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _handler.CreateAsync(
                _tenant.BusinessId, request, ct);
            return CreatedAtAction(nameof(Detail),
                new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // PUT /api/departments/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateDepartmentRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _handler.UpdateAsync(
                _tenant.BusinessId, id, request, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // DELETE /api/departments/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var (found, hasActiveIssues) = await _handler.DeleteAsync(
            _tenant.BusinessId, id, ct);

        if (!found) return NotFound();

        if (hasActiveIssues)
            return Conflict(new
            {
                error = "Cannot delete a department with active issues. " +
                        "Reassign or resolve all issues first."
            });

        return NoContent();
    }
}
