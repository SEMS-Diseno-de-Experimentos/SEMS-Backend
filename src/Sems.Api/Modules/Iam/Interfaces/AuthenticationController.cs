using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sems.Api.Modules.Iam.Application;
using Sems.Api.Shared.Errors;
using Sems.Api.Shared.Persistence;
using static Sems.Api.Modules.Iam.Interfaces.AuthResources;

namespace Sems.Api.Modules.Iam.Interfaces;

/// <summary>
/// API REST de autenticacion.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[Tags("Authentication")]
[AllowAnonymous]
public sealed class AuthenticationController : ControllerBase
{
    private readonly AuthenticationService _authentication;
    private readonly AccountRecoveryService _recovery;
    private readonly SystemDataSeeder _seeder;
    private readonly SemsDbContext _context;

    public AuthenticationController(AuthenticationService authentication,
        AccountRecoveryService recovery, SystemDataSeeder seeder, SemsDbContext context)
    {
        _authentication = authentication;
        _recovery = recovery;
        _seeder = seeder;
        _context = context;
    }

    private async Task CleanupUserInternalAsync(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        try
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dm_device_events WHERE device_id IN (SELECT id FROM dm_devices WHERE user_id IN (SELECT \"UserId\" FROM iam_users WHERE \"EmailAddress\" = {normalized}))");
            await _context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dm_device_configurations WHERE device_id IN (SELECT id FROM dm_devices WHERE user_id IN (SELECT \"UserId\" FROM iam_users WHERE \"EmailAddress\" = {normalized}))");
            await _context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dm_device_bindings WHERE device_id IN (SELECT id FROM dm_devices WHERE user_id IN (SELECT \"UserId\" FROM iam_users WHERE \"EmailAddress\" = {normalized}))");
            await _context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dm_devices WHERE user_id IN (SELECT \"UserId\" FROM iam_users WHERE \"EmailAddress\" = {normalized})");
            await _context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM em_energy_readings WHERE user_id = {normalized} OR user_id IN (SELECT \"UserId\"::text FROM iam_users WHERE \"EmailAddress\" = {normalized})");
            await _context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM em_alerts WHERE user_id = {normalized} OR user_id IN (SELECT \"UserId\"::text FROM iam_users WHERE \"EmailAddress\" = {normalized})");
            await _context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM em_meters WHERE user_id = {normalized} OR user_id IN (SELECT \"UserId\"::text FROM iam_users WHERE \"EmailAddress\" = {normalized})");
            await _context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM al_alerts WHERE user_id IN (SELECT \"UserId\" FROM iam_users WHERE \"EmailAddress\" = {normalized})");
            await _context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM org_memberships WHERE user_id IN (SELECT \"UserId\" FROM iam_users WHERE \"EmailAddress\" = {normalized})");
            await _context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM sb_subscriptions WHERE \"UserId\" IN (SELECT \"UserId\" FROM iam_users WHERE \"EmailAddress\" = {normalized})");
            await _context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM iam_refresh_tokens WHERE \"UserId\" IN (SELECT \"UserId\" FROM iam_users WHERE \"EmailAddress\" = {normalized})");
            await _context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM iam_user_auth_tokens WHERE \"UserId\" IN (SELECT \"UserId\" FROM iam_users WHERE \"EmailAddress\" = {normalized})");
            await _context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM iam_users WHERE \"EmailAddress\" = {normalized}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cleanup error for {normalized}: {ex.Message}");
        }
    }

    /// <summary>Deletes user and all related demo data (for testing/reset).</summary>
    [HttpDelete("test-cleanup")]
    public async Task<IActionResult> TestCleanup([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return BadRequest("email is required");
        await CleanupUserInternalAsync(email);
        return Ok(new { message = $"Usuario {email} limpiado correctamente." });
    }

    /// <summary>Creates a new account.</summary>
    [HttpPost("register")]
    public async Task<LoginResponse> Register([FromBody] RegisterRequest request)
    {
        var result = await _authentication.RegisterAsync(request.EmailAddress, request.Password,
            request.Role, request.FullName, request.Segment);
        try { await _seeder.SeedForUserAsync(request.EmailAddress); } catch { /* ignore seeding errors */ }
        return LoginResponse.From(result);
    }

    /// <summary>Signs in with email and password.</summary>
    [HttpPost("login")]
    public async Task<LoginResponse> Login([FromBody] LoginRequest request) =>
        LoginResponse.From(await _authentication.LoginAsync(request.EmailAddress, request.Password));

    /// <summary>Signs in with a Google account, creating it the first time.</summary>
    [HttpPost("google")]
    public async Task<LoginResponse> Google([FromBody] GoogleSignInRequest request,
        CancellationToken ct) =>
        LoginResponse.From(await _authentication.SignInWithGoogleAsync(request.IdToken, ct));

    /// <summary>Issues a new token pair and rotates the refresh token provided.</summary>
    [HttpPost("refresh")]
    public async Task<LoginResponse> Refresh([FromBody] RefreshRequest request) =>
        LoginResponse.From(await _recovery.RefreshAsync(request.RefreshToken));

    /// <summary>
    /// Signs out by revoking the refresh token.
    ///
    /// <para>Always answers 204: asking to close a session that no longer exists
    /// is not an error from the client's point of view.</para>
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request)
    {
        await _recovery.LogoutAsync(null, request?.RefreshToken);
        return NoContent();
    }

    /// <summary>Activates the account with the code received by email.</summary>
    [HttpPost("verify")]
    public async Task<LoginResponse> Verify([FromBody] VerifyRequest request) =>
        LoginResponse.From(await _recovery.VerifyAccountAsync(request.Token));

    /// <summary>
    /// Starts the password recovery flow.
    ///
    /// <para>Answers the same whether or not the account exists. Answering
    /// differently would turn this endpoint into a checker of registered
    /// emails.</para>
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<MessageResponse> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        await _recovery.ForgotPasswordAsync(request.EmailAddress);
        return new MessageResponse(
            "If that email is registered, you will receive a link to reset your password.");
    }

    /// <summary>Changes the password and closes every open session.</summary>
    [HttpPost("reset-password")]
    public async Task<MessageResponse> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        await _recovery.ResetPasswordAsync(request.Token, request.NewPassword);
        return new MessageResponse("Password updated successfully.");
    }
}
