using Microsoft.AspNetCore.Mvc;
using OnPoint.Infrastructure.Auth;

namespace OnPoint.API.Controllers;

[ApiController]
[Route("api/auth/staff")]
public class AuthController(StaffAuthHandler auth) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email)
            || string.IsNullOrWhiteSpace(req.Password)
            || string.IsNullOrWhiteSpace(req.FullName)
            || string.IsNullOrWhiteSpace(req.BusinessName))
            return BadRequest(new { error = "All fields required" });

        if (req.Password.Length < 8)
            return BadRequest(new { error = "Password must be at least 8 characters" });

        var result = await auth.RegisterAsync(req);

        if (result.Error == "EMAIL_TAKEN")
            return Conflict(new { error = "Email already registered" });
        if (result.Error == "INVALID_BUSINESS_TYPE")
            return BadRequest(new { error = "Invalid business type" });

        return StatusCode(201, new
        {
            userId = result.UserId,
            businessId = result.BusinessId,
            accessToken = result.AccessToken
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var result = await auth.LoginAsync(req.Email, req.Password);

        return result.Error switch
        {
            "INVALID_CREDENTIALS" => Unauthorized(new { error = "Invalid email or password" }),
            "ACCOUNT_LOCKED"      => StatusCode(423, new { error = "Account temporarily locked" }),
            "NO_BUSINESS"         => StatusCode(403, new { error = "No business associated" }),
            _                     => Ok(new
            {
                userId = result.UserId,
                businessId = result.BusinessId,
                accessToken = result.AccessToken
            })
        };
    }
}

public record LoginRequest(string Email, string Password);
