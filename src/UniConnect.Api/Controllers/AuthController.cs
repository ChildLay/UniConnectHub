using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UniConnect.Application.Auth;

namespace UniConnect.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController(IAuthenticationService authentication) : ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting("sensitive")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        await authentication.RegisterAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created);
    }

    [HttpPost("login")]
    [EnableRateLimiting("sensitive")]
    [ProducesResponseType<TokenPair>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenPair>> Login(LoginRequest request, CancellationToken cancellationToken) =>
        Ok(await authentication.LoginAsync(request, cancellationToken));

    [HttpPost("refresh")]
    [EnableRateLimiting("sensitive")]
    [ProducesResponseType<TokenPair>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenPair>> Refresh(RefreshRequest request, CancellationToken cancellationToken) =>
        Ok(await authentication.RefreshAsync(request.RefreshToken, cancellationToken));

    [HttpPost("verify-email")]
    [EnableRateLimiting("sensitive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> VerifyEmail(OneTimeTokenRequest request, CancellationToken cancellationToken)
    {
        await authentication.VerifyEmailAsync(request.Token, cancellationToken);
        return NoContent();
    }

    [HttpPost("resend-verification")]
    [EnableRateLimiting("sensitive")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ResendVerification(EmailRequest request, CancellationToken cancellationToken)
    {
        await authentication.ResendVerificationAsync(request.Email, cancellationToken);
        return Accepted();
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("sensitive")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword(EmailRequest request, CancellationToken cancellationToken)
    {
        await authentication.RequestPasswordResetAsync(request.Email, cancellationToken);
        return Accepted();
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("sensitive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await authentication.ResetPasswordAsync(request.Token, request.NewPassword, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken cancellationToken)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(claim, out var userId)) return Unauthorized();
        await authentication.LogoutAsync(userId, request.RefreshToken, cancellationToken);
        return NoContent();
    }
}

public sealed record RefreshRequest(string RefreshToken);
public sealed record OneTimeTokenRequest(string Token);
public sealed record EmailRequest(string Email);
public sealed record ResetPasswordRequest(string Token, string NewPassword);
