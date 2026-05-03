using Microsoft.AspNetCore.Mvc;
using OnPoint.Infrastructure.Feedback;

namespace OnPoint.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeedbackController : ControllerBase
{
    private readonly FeedbackHandler _handler;

    public FeedbackController(FeedbackHandler handler)
    {
        _handler = handler;
    }

    [HttpPost]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitFeedbackRequest request,
        CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue("op_session", out var sessionIdStr)
            || !Guid.TryParse(sessionIdStr, out var sessionId))
        {
            return Unauthorized(new { error = "No valid guest session." });
        }

        try
        {
            var result = await _handler.HandleAsync(request, sessionId, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
