using System.ComponentModel.DataAnnotations;
using Sems.Api.Modules.Iam.Application;

namespace Sems.Api.Modules.Iam.Interfaces;

/// <summary>
/// Contrato JSON del modulo de identidad, en camelCase como el servicio original
/// en Spring.
/// </summary>
public static class AuthResources
{
    // ------------------------------------------------------------- peticiones

    public sealed record RegisterRequest(
        [Required(ErrorMessage = "is required")]
        [EmailAddress(ErrorMessage = "must be a valid email")] string EmailAddress,
        [Required(ErrorMessage = "is required")]
        [MinLength(8, ErrorMessage = "must be at least 8 characters")] string Password,
        string? Role);

    public sealed record LoginRequest(
        [Required(ErrorMessage = "is required")] string EmailAddress,
        [Required(ErrorMessage = "is required")] string Password);

    public sealed record RefreshRequest(
        [Required(ErrorMessage = "is required")] string RefreshToken);

    public sealed record LogoutRequest(string? RefreshToken);

    public sealed record VerifyRequest(
        [Required(ErrorMessage = "is required")] string Token);

    public sealed record ForgotPasswordRequest(
        [Required(ErrorMessage = "is required")]
        [EmailAddress(ErrorMessage = "must be a valid email")] string EmailAddress);

    public sealed record ResetPasswordRequest(
        [Required(ErrorMessage = "is required")] string Token,
        [Required(ErrorMessage = "is required")]
        [MinLength(8, ErrorMessage = "must be at least 8 characters")] string NewPassword);

    // -------------------------------------------------------------- respuestas

    /// <summary>
    /// Respuesta de autenticacion.
    ///
    /// <para>Lleva dos tokens: el de acceso, de vida corta, que viaja en cada
    /// peticion, y el de refresco, de vida larga, que solo se usa para pedir uno
    /// nuevo. Separar ambos limita el danio si el de acceso se filtra.</para>
    /// </summary>
    public sealed record LoginResponse(string Token, string RefreshToken, Guid UserId,
        string EmailAddress, List<string> Roles)
    {
        public static LoginResponse From(SessionResult s) =>
            new(s.Token, s.RefreshToken, s.UserId, s.EmailAddress, s.Roles);
    }

    public sealed record MessageResponse(string Message);
}
