using Sems.Api.Modules.Iam.Domain.Model;
using Sems.Api.Modules.Iam.Domain.Repositories;
using Sems.Api.Modules.Iam.Domain.Services;
using Sems.Api.Shared.Errors;

namespace Sems.Api.Modules.Iam.Application;

/// <summary>Sesion entregada tras autenticarse.</summary>
public sealed record SessionResult(string Token, string RefreshToken, Guid UserId,
    string EmailAddress, List<string> Roles);

/// <summary>
/// Alta e inicio de sesion.
///
/// <para>Orquesta repositorios, servicios de dominio y el publicador de eventos,
/// pero no contiene reglas de negocio: esas viven en el dominio.</para>
/// </summary>
public sealed class AuthenticationService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHashingService _hashing;
    private readonly ITokenService _tokens;
    private readonly AuthTokenService _authTokens;
    private readonly IIamEventPublisher _events;
    private readonly bool _requireVerification;

    public AuthenticationService(IUserRepository users, IPasswordHashingService hashing,
        ITokenService tokens, AuthTokenService authTokens, IIamEventPublisher events,
        IConfiguration configuration)
    {
        _users = users;
        _hashing = hashing;
        _tokens = tokens;
        _authTokens = authTokens;
        _events = events;
        _requireVerification = string.Equals(
            configuration["Security:RequireVerification"]
            ?? Environment.GetEnvironmentVariable("REQUIRE_VERIFICATION"),
            "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<SessionResult> RegisterAsync(string? emailAddress, string? password,
        string? role, CancellationToken ct = default)
    {
        var email = new EmailAddress(emailAddress);

        if (await _users.ExistsByEmailAsync(email.Value, ct))
        {
            throw AppException.Conflict("Email already exists");
        }
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw AppException.Validation("password must be at least 8 characters");
        }

        var user = await _users.SaveAsync(User.Register(email, _hashing.Hash(password),
            RoleNameExtensions.ToRoleName(role), _requireVerification), ct);

        _events.PublishUserRegistered(user.UserId, user.EmailAddress, user.Role.ToString());

        // El codigo de verificacion se emite y se pide por evento: el envio del
        // correo ocurre en el modulo de notificaciones, tras confirmar.
        if (_requireVerification)
        {
            var verificationToken = await _authTokens.IssueVerificationTokenAsync(user.UserId, ct);
            _events.PublishVerificationRequested(user.UserId, user.EmailAddress, verificationToken);
        }

        return await BuildSessionAsync(user, ct);
    }

    public async Task<SessionResult> LoginAsync(string? emailAddress, string? password,
        CancellationToken ct = default)
    {
        var email = new EmailAddress(emailAddress);

        // Cuando el correo no existe O la contrasena es incorrecta se lanza el
        // MISMO error a proposito. Un mensaje distinto revelaria que correos
        // estan registrados.
        var user = await _users.FindByEmailAsync(email.Value, ct)
                   ?? throw AppException.Unauthorized("Invalid credentials");

        if (string.IsNullOrEmpty(password) || !_hashing.Matches(password, user.PasswordHash))
        {
            throw AppException.Unauthorized("Invalid credentials");
        }

        // Una cuenta sin verificar no entra. El mensaje si es explicito aqui
        // porque las credenciales ya se comprobaron: no filtra nada.
        if (_requireVerification && user.IsPending)
        {
            throw AppException.Unauthorized("Account is not verified yet");
        }

        _events.PublishUserLoggedIn(user.UserId, user.EmailAddress);
        return await BuildSessionAsync(user, ct);
    }

    /// <summary>Arma el par de tokens y la respuesta de sesion para un usuario.</summary>
    public async Task<SessionResult> BuildSessionAsync(User user, CancellationToken ct = default)
    {
        var accessToken = _tokens.GenerateToken(user);
        var refreshToken = await _authTokens.IssueRefreshTokenAsync(user.UserId, ct);

        return new SessionResult(accessToken, refreshToken, user.UserId, user.EmailAddress,
            new List<string> { user.Role.ToString() });
    }
}

/// <summary>
/// Verificacion de cuenta, recuperacion de contrasena, refresco y cierre de sesion.
///
/// <para>Se separa de <see cref="AuthenticationService"/> para que ese no siga
/// creciendo: alli viven el alta y el inicio de sesion; aqui, el ciclo de vida
/// de la credencial.</para>
/// </summary>
public sealed class AccountRecoveryService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHashingService _hashing;
    private readonly AuthTokenService _authTokens;
    private readonly AuthenticationService _authentication;
    private readonly IIamEventPublisher _events;
    private readonly ILogger<AccountRecoveryService> _logger;

    public AccountRecoveryService(IUserRepository users, IPasswordHashingService hashing,
        AuthTokenService authTokens, AuthenticationService authentication,
        IIamEventPublisher events, ILogger<AccountRecoveryService> logger)
    {
        _users = users;
        _hashing = hashing;
        _authTokens = authTokens;
        _authentication = authentication;
        _events = events;
        _logger = logger;
    }

    /// <summary>
    /// Entrega un par de tokens nuevo a partir de uno de refresco valido.
    ///
    /// <para>El de refresco se rota en el proceso: el anterior queda revocado.</para>
    /// </summary>
    public async Task<SessionResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var userId = await _authTokens.ConsumeRefreshTokenAsync(refreshToken, ct);
        var user = await _users.FindByIdAsync(userId, ct)
                   ?? throw AppException.Unauthorized("Invalid refresh token");

        return await _authentication.BuildSessionAsync(user, ct);
    }

    /// <summary>Cierra la sesion. Sin token concreto, cierra todas las del usuario.</summary>
    public Task LogoutAsync(Guid? userId, string? refreshToken, CancellationToken ct = default) =>
        _authTokens.RevokeAsync(userId, refreshToken, ct);

    /// <summary>Activa la cuenta y devuelve una sesion, para que el usuario entre directo.</summary>
    public async Task<SessionResult> VerifyAccountAsync(string token, CancellationToken ct = default)
    {
        var userId = await _authTokens.ConsumeSingleUseAsync(token,
            UserAuthToken.PurposeVerification, ct);

        var user = await _users.FindByIdAsync(userId, ct)
                   ?? throw AppException.NotFound("User not found");

        user.Activate();
        await _users.SaveAsync(user, ct);

        return await _authentication.BuildSessionAsync(user, ct);
    }

    /// <summary>
    /// Inicia la recuperacion de contrasena.
    ///
    /// <para><b>No revela si el correo existe.</b> El metodo termina en silencio
    /// cuando no hay cuenta asociada, y el controlador responde siempre lo
    /// mismo. Contestar distinto convertiria este endpoint en un verificador de
    /// correos registrados para cualquiera que lo consulte.</para>
    /// </summary>
    public async Task ForgotPasswordAsync(string? emailAddress, CancellationToken ct = default)
    {
        var email = new EmailAddress(emailAddress);
        var user = await _users.FindByEmailAsync(email.Value, ct);

        if (user is null)
        {
            _logger.LogInformation(
                "Recuperacion solicitada para un correo no registrado; no se envia nada");
            return;
        }

        var token = await _authTokens.IssuePasswordResetTokenAsync(user.UserId, ct);
        _events.PublishPasswordResetRequested(user.UserId, user.EmailAddress, token);
    }

    /// <summary>
    /// Cambia la contrasena y cierra todas las sesiones abiertas.
    ///
    /// <para>Revocar los tokens es parte del caso de uso: si el usuario cambia la
    /// contrasena porque sospecha que alguien entro, dejar viva la sesion del
    /// intruso vaciaria de sentido la operacion.</para>
    /// </summary>
    public async Task ResetPasswordAsync(string token, string newPassword,
        CancellationToken ct = default)
    {
        var userId = await _authTokens.ConsumeSingleUseAsync(token,
            UserAuthToken.PurposePasswordReset, ct);

        var user = await _users.FindByIdAsync(userId, ct)
                   ?? throw AppException.NotFound("User not found");

        user.ChangePassword(_hashing.Hash(newPassword));
        await _users.SaveAsync(user, ct);

        await _authTokens.RevokeAsync(userId, null, ct);
    }
}
