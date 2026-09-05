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

    /// <summary>Crea una cuenta nueva.</summary>
    [HttpPost("register")]
    public async Task<LoginResponse> Register([FromBody] RegisterRequest request) =>
        LoginResponse.From(await _authentication.RegisterAsync(request.EmailAddress,
            request.Password, request.Role));

    /// <summary>Inicia sesion con correo y contrasena.</summary>
    [HttpPost("login")]
    public async Task<LoginResponse> Login([FromBody] LoginRequest request) =>
        LoginResponse.From(await _authentication.LoginAsync(request.EmailAddress, request.Password));

    /// <summary>Entrega un par de tokens nuevo y rota el de refresco entregado.</summary>
    [HttpPost("refresh")]
    public async Task<LoginResponse> Refresh([FromBody] RefreshRequest request) =>
        LoginResponse.From(await _recovery.RefreshAsync(request.RefreshToken));

    /// <summary>
    /// Cierra la sesion revocando el token de refresco.
    ///
    /// <para>Responde 204 siempre: pedir el cierre de una sesion que ya no existe
    /// no es un error desde el punto de vista del cliente.</para>
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request)
    {
        await _recovery.LogoutAsync(null, request?.RefreshToken);
        return NoContent();
    }

    /// <summary>Activa la cuenta con el codigo recibido por correo.</summary>
    [HttpPost("verify")]
    public async Task<LoginResponse> Verify([FromBody] VerifyRequest request) =>
        LoginResponse.From(await _recovery.VerifyAccountAsync(request.Token));

    /// <summary>
    /// Inicia la recuperacion de contrasena.
    ///
    /// <para>Responde lo mismo exista o no la cuenta. Contestar distinto
    /// convertiria este endpoint en un verificador de correos registrados.</para>
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<MessageResponse> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        await _recovery.ForgotPasswordAsync(request.EmailAddress);
        return new MessageResponse(
            "Si el correo esta registrado, recibiras un enlace para cambiar tu contrasena.");
    }

    /// <summary>Cambia la contrasena y cierra todas las sesiones abiertas.</summary>
    [HttpPost("reset-password")]
    public async Task<MessageResponse> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        await _recovery.ResetPasswordAsync(request.Token, request.NewPassword);
        return new MessageResponse("Contrasena actualizada correctamente.");
    }
}
