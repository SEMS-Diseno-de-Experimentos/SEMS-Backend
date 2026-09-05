using System.Security.Cryptography;
using System.Text;
using Sems.Api.Modules.Iam.Domain.Model;
using Sems.Api.Modules.Iam.Domain.Repositories;
using Sems.Api.Shared.Errors;

namespace Sems.Api.Modules.Iam.Application;

/// <summary>
/// Emision y consumo de tokens opacos: refresco, verificacion y recuperacion.
///
/// <para>Tres decisiones de seguridad que conviene no perder de vista:</para>
/// <list type="number">
///   <item><b>Se guarda el resumen SHA-256, nunca el token.</b> Una copia de la
///   base de datos no permite suplantar a nadie.</item>
///   <item><b>El valor se genera con <see cref="RandomNumberGenerator"/></b>, no
///   con <c>Random</c> ni con un GUID: 32 bytes de entropia criptografica.</item>
///   <item><b>Los tokens de un solo uso se marcan como usados.</b> Un enlace de
///   recuperacion reenviado o archivado deja de servir tras el primer uso.</item>
/// </list>
/// </summary>
public sealed class AuthTokenService
{
    /// <summary>Ventana de validez del enlace de recuperacion de contrasena.</summary>
    private static readonly TimeSpan ResetTtl = TimeSpan.FromHours(1);

    /// <summary>Ventana de validez del codigo de verificacion de cuenta.</summary>
    private static readonly TimeSpan VerificationTtl = TimeSpan.FromHours(24);

    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUserAuthTokenRepository _authTokens;
    private readonly int _refreshExpirationDays;

    public AuthTokenService(IRefreshTokenRepository refreshTokens,
        IUserAuthTokenRepository authTokens, IConfiguration configuration)
    {
        _refreshTokens = refreshTokens;
        _authTokens = authTokens;
        _refreshExpirationDays = int.TryParse(
            configuration["Security:Jwt:RefreshExpirationDays"]
            ?? Environment.GetEnvironmentVariable("JWT_REFRESH_EXPIRATION_DAYS"),
            out var d) ? d : 30;
    }

    /// <summary>
    /// Emite un token de refresco y devuelve su valor en claro.
    ///
    /// <para>Es la unica vez que ese valor existe fuera del navegador del
    /// usuario: en base de datos solo queda el resumen.</para>
    /// </summary>
    public async Task<string> IssueRefreshTokenAsync(Guid userId, CancellationToken ct = default)
    {
        var raw = RandomToken();
        await _refreshTokens.SaveAsync(RefreshToken.Issue(userId, Sha256(raw),
            DateTime.UtcNow.AddDays(_refreshExpirationDays)), ct);
        return raw;
    }

    /// <summary>
    /// Valida un token de refresco y lo rota.
    ///
    /// <para>La rotacion es deliberada: el token usado se revoca y se entrega uno
    /// nuevo. Si alguien robara uno y lo usara, el legitimo dejaria de funcionar
    /// y el robo se haria visible.</para>
    /// </summary>
    /// <returns>el identificador del usuario dueno del token</returns>
    public async Task<Guid> ConsumeRefreshTokenAsync(string rawToken, CancellationToken ct = default)
    {
        var stored = await _refreshTokens.FindByHashAsync(Sha256(rawToken), ct)
                     ?? throw AppException.Unauthorized("Invalid refresh token");

        if (stored.Revoked)
        {
            throw AppException.Unauthorized("Refresh token was revoked");
        }
        if (stored.ExpiresAt <= DateTime.UtcNow)
        {
            throw AppException.Unauthorized("Refresh token expired");
        }

        stored.Revoke();
        await _refreshTokens.SaveAsync(stored, ct);
        return stored.UserId;
    }

    /// <summary>Revoca un token concreto; si no se indica, cierra todas las sesiones.</summary>
    public async Task RevokeAsync(Guid? userId, string? rawToken, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(rawToken))
        {
            var stored = await _refreshTokens.FindByHashAsync(Sha256(rawToken), ct);
            if (stored is not null)
            {
                stored.Revoke();
                await _refreshTokens.SaveAsync(stored, ct);
            }
            return;
        }

        if (userId is not null)
        {
            await _refreshTokens.RevokeAllForUserAsync(userId.Value, ct);
        }
    }

    public Task<string> IssueVerificationTokenAsync(Guid userId, CancellationToken ct = default) =>
        IssueSingleUseAsync(userId, UserAuthToken.PurposeVerification, VerificationTtl, ct);

    public Task<string> IssuePasswordResetTokenAsync(Guid userId, CancellationToken ct = default) =>
        IssueSingleUseAsync(userId, UserAuthToken.PurposePasswordReset, ResetTtl, ct);

    /// <summary>Valida y marca como usado un token de un solo uso.</summary>
    /// <returns>el identificador del usuario dueno del token</returns>
    public async Task<Guid> ConsumeSingleUseAsync(string rawToken, string purpose,
        CancellationToken ct = default)
    {
        var stored = await _authTokens.FindByHashAndPurposeAsync(Sha256(rawToken), purpose, ct)
                     ?? throw AppException.Unauthorized("Invalid or unknown token");

        if (stored.UsedAt is not null)
        {
            throw AppException.Unauthorized("Token was already used");
        }
        if (stored.ExpiresAt <= DateTime.UtcNow)
        {
            throw AppException.Unauthorized("Token expired");
        }

        stored.MarkUsed();
        await _authTokens.SaveAsync(stored, ct);
        return stored.UserId;
    }

    private async Task<string> IssueSingleUseAsync(Guid userId, string purpose, TimeSpan ttl,
        CancellationToken ct)
    {
        var raw = RandomToken();
        await _authTokens.SaveAsync(UserAuthToken.Issue(userId, Sha256(raw), purpose,
            DateTime.UtcNow.Add(ttl)), ct);
        return raw;
    }

    /// <summary>32 bytes de entropia en base64 apta para URL.</summary>
    private static string RandomToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string Sha256(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
