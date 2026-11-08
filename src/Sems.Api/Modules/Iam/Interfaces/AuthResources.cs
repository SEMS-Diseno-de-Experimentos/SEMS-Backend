using System.ComponentModel.DataAnnotations;
using Sems.Api.Modules.Iam.Application;

namespace Sems.Api.Modules.Iam.Interfaces;

/// <summary>
/// JSON contract of the identity module, in camelCase as in the original
/// Spring service.
/// </summary>
public static class AuthResources
{
    // ------------------------------------------------------------- peticiones

    public sealed class RegisterRequest
    {
        private string? _emailAddress;

        [Required(ErrorMessage = "is required")]
        [EmailAddress(ErrorMessage = "must be a valid email")]
        public string EmailAddress
        {
            get => !string.IsNullOrWhiteSpace(_emailAddress) ? _emailAddress : (Email ?? string.Empty);
            set => _emailAddress = value;
        }

        public string? Email
        {
            get => _emailAddress;
            set => _emailAddress ??= value;
        }

        [Required(ErrorMessage = "is required")]
        [MinLength(8, ErrorMessage = "must be at least 8 characters")]
        public string Password { get; set; } = string.Empty;

        public string? Role { get; set; }

        [MaxLength(120, ErrorMessage = "must be at most 120 characters")]
        public string? FullName { get; set; }

        public string? Segment { get; set; }
    }

    public sealed class LoginRequest
    {
        private string? _emailAddress;

        [Required(ErrorMessage = "is required")]
        public string EmailAddress
        {
            get => !string.IsNullOrWhiteSpace(_emailAddress) ? _emailAddress : (Email ?? string.Empty);
            set => _emailAddress = value;
        }

        public string? Email
        {
            get => _emailAddress;
            set => _emailAddress ??= value;
        }

        [Required(ErrorMessage = "is required")]
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// Token de identidad que devuelve el boton de Google en el navegador.
    /// </summary>
    public sealed record GoogleSignInRequest(
        [Required(ErrorMessage = "is required")] string IdToken);

    public sealed record RefreshRequest(
        [Required(ErrorMessage = "is required")] string RefreshToken);

    public sealed record LogoutRequest(string? RefreshToken);

    public sealed record VerifyRequest(
        [Required(ErrorMessage = "is required")] string Token);

    public sealed class ForgotPasswordRequest
    {
        private string? _emailAddress;

        [Required(ErrorMessage = "is required")]
        [EmailAddress(ErrorMessage = "must be a valid email")]
        public string EmailAddress
        {
            get => !string.IsNullOrWhiteSpace(_emailAddress) ? _emailAddress : (Email ?? string.Empty);
            set => _emailAddress = value;
        }

        public string? Email
        {
            get => _emailAddress;
            set => _emailAddress ??= value;
        }
    }

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
        string EmailAddress, List<string> Roles, string? FullName, string? Segment)
    {
        public static LoginResponse From(SessionResult s) =>
            new(s.Token, s.RefreshToken, s.UserId, s.EmailAddress, s.Roles, s.FullName, s.Segment);
    }

    public sealed record UpdateProfileRequest(
        [MaxLength(120, ErrorMessage = "must be at most 120 characters")] string? FullName,
        string? Segment);

    public sealed record MessageResponse(string Message);
}
