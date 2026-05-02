using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnPoint.Infrastructure.Persistence;

namespace OnPoint.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeedbackController(AppDbContext db) : ControllerBase
{
    // Temporary: verify DB is reachable and tables exist
    [HttpGet("ping")]
    public async Task<IActionResult> Ping()
    {
        var businessCount = await db.Businesses.CountAsync();
        return Ok(new { status = "db_connected", businessCount });
    }
}
