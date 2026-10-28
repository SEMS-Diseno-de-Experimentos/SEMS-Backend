using Sems.Api.Modules.Iam.Domain.Model;
using Sems.Api.Modules.Iam.Domain.Repositories;
using Sems.Api.Shared.Errors;

namespace Sems.Api.Modules.Iam.Application;

/// <summary>
/// Cambios que el usuario hace sobre su propio perfil.
/// </summary>
/// <remarks>
/// Esta aparte de la autenticacion a proposito: cambiar el nombre no toca
/// credenciales ni roles, y mezclarlo con el alta y el login haria pasar por
/// ese codigo una operacion que no necesita ninguna de sus comprobaciones.
/// </remarks>
public sealed class UserProfileService
{
    private readonly IUserRepository _users;

    public UserProfileService(IUserRepository users) => _users = users;

    /// <summary>Guarda el nombre y el segmento del usuario indicado.</summary>
    public async Task<User> UpdateProfileAsync(Guid userId, string? fullName, string? segment,
        CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId, ct)
                   ?? throw AppException.NotFound("user not found");

        user.UpdateProfile(fullName, UserSegmentExtensions.ToUserSegment(segment));
        return await _users.SaveAsync(user, ct);
    }
}
