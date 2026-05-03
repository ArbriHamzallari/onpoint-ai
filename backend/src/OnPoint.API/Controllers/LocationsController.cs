using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnPoint.Application.Tenancy;
using OnPoint.Infrastructure.Locations;

namespace OnPoint.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LocationsController : ControllerBase
{
    private readonly LocationHandler _handler;
    private readonly ITenantContext _tenant;

    public LocationsController(
        LocationHandler handler,
        ITenantContext tenant)
    {
        _handler = handler;
        _tenant = tenant;
    }

    // GET /api/locations?search=101&includeInactive=false&page=1&pageSize=20
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] LocationListRequest request,
        CancellationToken ct)
    {
        var result = await _handler.ListAsync(
            _tenant.BusinessId, request, ct);
        return Ok(result);
    }

    // GET /api/locations/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var result = await _handler.GetDetailAsync(
            _tenant.BusinessId, id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    // POST /api/locations
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateLocationRequest request,
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

    // PUT /api/locations/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateLocationRequest request,
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

    // DELETE /api/locations/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var (found, hasActiveIssues) = await _handler.DeleteAsync(
            _tenant.BusinessId, id, ct);

        if (!found) return NotFound();

        if (hasActiveIssues)
            return Conflict(new
            {
                error = "Cannot delete a room with active issues. " +
                        "Resolve or cancel all issues first."
            });

        return NoContent();
    }

    // GET /api/locations/{id}/qr
    [HttpGet("{id:guid}/qr")]
    public async Task<IActionResult> QrCode(Guid id, CancellationToken ct)
    {
        var (png, found) = await _handler.GetQrCodeAsync(
            _tenant.BusinessId, id, ct);

        if (!found) return NotFound();

        return File(png!, "image/png", $"qr-{id}.png");
    }
}
