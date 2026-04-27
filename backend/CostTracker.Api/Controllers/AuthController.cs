using System.Security.Claims;
using CostTracker.Api.Configuration;
using CostTracker.Application.Contracts;
using CostTracker.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CostTracker.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public class AuthController(
    IOptions<AuthOptions> authOptions,
    PasswordHashService passwordHashService) : ControllerBase
{
    [HttpGet("session")]
    public ActionResult<AuthSessionDto> GetSession()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Ok(new AuthSessionDto(false, null));
        }

        return Ok(new AuthSessionDto(true, User.Identity.Name));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthSessionDto>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("username and password are required.");
        }

        var configuredUsername = authOptions.Value.Username;
        var configuredPasswordHash = authOptions.Value.PasswordHash;

        var isValidUsername = string.Equals(request.Username.Trim(), configuredUsername, StringComparison.Ordinal);
        var isValidPassword = isValidUsername &&
            passwordHashService.VerifyPassword(configuredUsername, configuredPasswordHash, request.Password);

        if (!isValidPassword)
        {
            return Unauthorized("Invalid credentials.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, configuredUsername)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
            });

        return Ok(new AuthSessionDto(true, configuredUsername));
    }

    [HttpPost("logout")]
    public async Task<ActionResult<AuthSessionDto>> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new AuthSessionDto(false, null));
    }
}
