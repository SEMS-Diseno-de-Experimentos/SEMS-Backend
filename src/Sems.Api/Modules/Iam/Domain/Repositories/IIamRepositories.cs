using Sems.Api.Modules.Iam.Domain.Model;

namespace Sems.Api.Modules.Iam.Domain.Repositories;

/// <summary>Puertos de salida del modulo de identidad.</summary>
public interface IUserRepository
{
    Task<User> SaveAsync(User user, CancellationToken ct = default);
    Task<User?> FindByIdAsync(Guid userId, CancellationToken ct = default);
    Task<User?> FindByEmailAsync(string emailAddress, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string emailAddress, CancellationToken ct = default);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken> SaveAsync(RefreshToken token, CancellationToken ct = default);
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default);
    /// <summary>Cierra la sesion en todos los dispositivos del usuario.</summary>
    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);
}

public interface IUserAuthTokenRepository
{
    Task<UserAuthToken> SaveAsync(UserAuthToken token, CancellationToken ct = default);
    Task<UserAuthToken?> FindByHashAndPurposeAsync(string tokenHash, string purpose, CancellationToken ct = default);
}
