using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sems.Api.Modules.Iam.Application;
using static Sems.Api.Modules.Iam.Interfaces.AuthResources;

namespace Sems.Api.Modules.Iam.Interfaces;

/// <summary>
/// API REST de autenticacion.
///
/// <para>En la capa de interfaces el controlador solo maneja HTTP: lee la
/// peticion, delega en el servicio de aplicacion y devuelve la respuesta. No
/// contiene logica de negocio.</para>
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[Tags("Authentication")]
[AllowAnonymous]
public sealed class AuthenticationController : ControllerBase
{
    private readonly AuthenticationService _authentication;
    private readonly AccountRecoveryService _recovery;

    public AuthenticationController(AuthenticationService authentication,
        AccountRecoveryService recovery)
    {
        _authentication = authentication;
        _recovery = recovery;
    }

    /// <summary>Creates a new account.</summary>
    [HttpPost("register")]
    public async Task<LoginResponse> Register([FromBody] RegisterRequest request) =>
        LoginResponse.From(await _authentication.RegisterAsync(request.EmailAddress,
            request.Password, request.Role));

    /// <summary>Signs in with email and password.</summary>
    [HttpPost("login")]
    public async Task<LoginResponse> Login([FromBody] LoginRequest request) =>
        LoginResponse.From(await _authentication.LoginAsync(request.EmailAddress, request.Password));

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
        return new MessageResponse("Contrasena actualizada correctamente.");
    }
}
