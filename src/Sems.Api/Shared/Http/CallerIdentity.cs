using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Sems.Api.Shared.Errors;

namespace Sems.Api.Shared.Http;

/// <summary>
/// Identidad de quien hace la peticion, leida del token.
///
/// <para>La politica de <c>Program.cs</c> ya garantiza que toda peticion llega
/// autenticada, pero autenticar solo dice <i>quien</i> llama. Estos ayudantes
/// cubren la otra mitad: que el identificador de usuario que trae la ruta o el
/// cuerpo sea el de quien llama, y no el de un tercero.</para>
///
/// <para>Viven en la capa HTTP y no en la de aplicacion porque el identificador
/// sospechoso es el que manda el cliente. Los servicios siguen aceptando
/// cualquier usuario, que es lo que necesitan el sembrador de datos y los
/// manejadores de eventos de dominio, que no llaman en nombre de nadie.</para>
/// </summary>
public static class CallerIdentity
{
    /// <summary>El usuario dueño del token.</summary>
    /// <remarks>
    /// Se consultan los dos nombres posibles porque JwtBearer traduce <c>sub</c>
    /// a <see cref="ClaimTypes.NameIdentifier"/> cuando el mapeo de nombres esta
    /// activo, y lo deja tal cual cuando no lo esta. Mirar solo uno funciona
    /// hasta que alguien cambia esa opcion.
    /// </remarks>
    public static Guid CallerId(this ClaimsPrincipal caller)
    {
        var valor = caller.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? caller.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(valor, out var id)
            ? id
            : throw AppException.Unauthorized("token does not carry a valid user identifier");
    }

    /// <summary>
    /// Comprueba que <paramref name="userId"/> es el del token y lo devuelve.
    /// </summary>
    /// <remarks>
    /// Devuelve el identificador para que el sitio de llamada no tenga que
    /// repetirlo: asi la comprobacion no se puede olvidar sin que ademas falte
    /// el dato con el que se consulta.
    /// </remarks>
    public static Guid MustBeSelf(this ClaimsPrincipal caller, Guid userId)
    {
        var propio = caller.CallerId();
        if (userId != propio)
        {
            // El mensaje no dice de quien es el dato ni si existe: eso ya seria
            // contar algo de un tercero a quien no le corresponde preguntar.
            throw AppException.Forbidden("the requested user is not the authenticated one");
        }
        return propio;
    }

    /// <summary>
    /// Igual que <see cref="MustBeSelf(ClaimsPrincipal, Guid)"/> cuando el
    /// identificador llega como texto, en la ruta o en el cuerpo.
    /// </summary>
    /// <remarks>
    /// Un identificador mal formado es un 400 y no un 403: el cliente se
    /// equivoco al escribirlo, no intento leer lo de otro.
    /// </remarks>
    public static Guid MustBeSelf(this ClaimsPrincipal caller, string? userId, string field = "user_id")
    {
        if (!Guid.TryParse(userId?.Trim(), out var id))
        {
            throw AppException.Validation($"{field} is not a valid identifier");
        }
        return caller.MustBeSelf(id);
    }
}
