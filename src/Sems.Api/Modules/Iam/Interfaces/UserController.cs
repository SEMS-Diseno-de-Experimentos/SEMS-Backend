using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Sems.Api.Modules.Iam.Domain.Repositories;
using Sems.Api.Shared.Errors;

namespace Sems.Api.Modules.Iam.Interfaces;

/// <summary>Details of the user the request token belongs to.</summary>
public sealed record UserResource(Guid UserId, string EmailAddress, List<string> Roles);

/// <summary>
/// Consulta del usuario autenticado.
///
/// <para>El cliente conoce su identificador porque se lo devolvio el inicio de
/// sesion, pero ese dato caduca con el token y no sirve para saber si el papel
/// del usuario cambio despues. Este recurso resuelve la identidad desde el
/// token en cada peticion, que es la unica fuente en la que el cliente no
/// puede influir.</para>
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Tags("Users")]
public sealed class UserController : ControllerBase
{
    private readonly IUserRepository _users;

    public UserController(IUserRepository users) => _users = users;

    /// <summary>Returns the user who owns the token used to call.</summary>
    [HttpGet("me")]
    public async Task<UserResource> Me(CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(UsuarioDelToken(), ct)
            ?? throw AppException.NotFound("user not found");
        return new UserResource(user.UserId, user.EmailAddress,
            new List<string> { user.Role.ToString() });
    }

    /// <summary>
    /// Lee el identificador del usuario desde el token.
    ///
    /// <para>Se consultan los dos nombres posibles porque JwtBearer traduce
    /// <c>sub</c> a <see cref="ClaimTypes.NameIdentifier"/> cuando el mapeo de
    /// nombres esta activo, y lo deja tal cual cuando no lo esta. Mirar solo uno
    /// funciona hasta que alguien cambia esa opcion.</para>
    /// </summary>
    private Guid UsuarioDelToken()
    {
        var valor = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(valor, out var id)
            ? id
            : throw AppException.Unauthorized("token does not carry a valid user identifier");
    }
}
