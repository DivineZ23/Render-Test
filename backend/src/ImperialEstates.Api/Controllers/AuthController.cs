using System.Security.Cryptography;
using ImperialEstates.Api.Extensions;
using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Application.Services;
using ImperialEstates.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ImperialEstates.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IDiscordOAuthService discord, AuthService auth, UserManagementService users, IConfiguration configuration, IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("discord")]
    [EnableRateLimiting("auth")]
    public Task<IActionResult> Discord([FromQuery] string? code, [FromQuery] string? state, CancellationToken ct)
    {
        if (code is not null || state is not null)
            return CompleteDiscordCallbackAsync(code, state, ct);

        var generatedState = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        Response.Cookies.Append("discord_oauth_state", generatedState, CookieOptions(TimeSpan.FromMinutes(10), sameSite: SameSiteMode.Lax));
        return Task.FromResult<IActionResult>(Redirect(discord.BuildAuthorizationUrl(generatedState)));
    }

    [HttpGet("discord/callback")]
    [EnableRateLimiting("auth")]
    public Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, CancellationToken ct) =>
        CompleteDiscordCallbackAsync(code, state, ct);

    private async Task<IActionResult> CompleteDiscordCallbackAsync(string? code, string? state, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            return BadRequest(new { statusCode = 400, errorCode = "INVALID_OAUTH_CALLBACK", message = "The OAuth callback must include both code and state.", traceId = HttpContext.TraceIdentifier });

        if (!Request.Cookies.TryGetValue("discord_oauth_state", out var expected) || !CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(state), System.Text.Encoding.UTF8.GetBytes(expected)))
            return BadRequest(new { statusCode = 400, errorCode = "INVALID_OAUTH_STATE", message = "The sign-in request could not be verified.", traceId = HttpContext.TraceIdentifier });
        Response.Cookies.Delete("discord_oauth_state");
        var result = await auth.SignInAsync(await discord.ExchangeCodeAsync(code, ct), ct);
        Response.Cookies.Append("imperial_auth", result.AccessToken, CookieOptions(result.ExpiresAt - DateTime.UtcNow, SameSiteMode.Lax));
        var frontend = !environment.IsDevelopment() && TryGetDiscordCallbackUri(out var callbackUri)
            ? callbackUri.GetLeftPart(UriPartial.Authority)
            : configuration["App:FrontendUrl"]?.TrimEnd('/') ?? "http://localhost:4200";
        var path = result.User.ApprovalStatus switch
        {
            ApprovalStatus.Pending => "/pending-approval",
            ApprovalStatus.Rejected => "/access-revoked",
            _ when result.User.AccessStatus == AccessStatus.Revoked => "/access-revoked",
            _ => "/dashboard"
        };
        return Redirect(frontend + path);
    }

    [Authorize]
    [HttpGet("me")]
    public Task<UserDto> Me(CancellationToken ct) => users.GetAsync(User.UserId(), ct);

    [Authorize]
    [HttpPut("me/profile")]
    public Task<UserDto> UpdateProfile(UpdateUserProfileRequest request, CancellationToken ct) =>
        users.UpdateProfileAsync(User.UserId(), request, ct);

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("imperial_auth");
        return NoContent();
    }

    private CookieOptions CookieOptions(TimeSpan lifetime, SameSiteMode sameSite) => new()
    {
        HttpOnly = true, Secure = !environment.IsDevelopment(),
        SameSite = sameSite, IsEssential = true, Expires = DateTimeOffset.UtcNow.Add(lifetime), Path = "/"
    };

    private bool TryGetDiscordCallbackUri(out Uri callbackUri) =>
        Uri.TryCreate(configuration["Discord:RedirectUri"], UriKind.Absolute, out callbackUri!);
}
