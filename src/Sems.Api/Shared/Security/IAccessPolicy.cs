using Sems.Api.Shared.Errors;

namespace Sems.Api.Shared.Security;

/// <summary>
/// Si quien llama alcanza un recurso, segun sus vinculos con la organizacion.
///
/// <para>El dato de un local no es de quien lo dio de alta: es de la
/// organizacion. El jefe de tienda tiene que ver el consumo de su local aunque
/// los medidores los registrara un operario, y no tiene por que ver el del
/// local de al lado. Eso no se puede responder comparando identificadores de
/// usuario; hace falta preguntar por las membresias, que es lo que hace este
/// puerto.</para>
///
/// <para>Vive en <c>Shared</c> y no en cada modulo consumidor, que es donde el
/// proyecto pone sus otros puertos. La razon es que aqui la pregunta es
/// identica para los siete modulos, y una regla de seguridad repetida siete
/// veces es una regla que acabara divergiendo en siete sitios.</para>
/// </summary>
public interface IAccessPolicy
{
    /// <summary>Si el llamante tiene alcance sobre ese local.</summary>
    Task<bool> ReachesSiteAsync(Guid caller, Guid siteId, CancellationToken ct = default);

    /// <summary>Si el llamante tiene alcance sobre el local al que pertenece la zona.</summary>
    Task<bool> ReachesZoneAsync(Guid caller, Guid zoneId, CancellationToken ct = default);

    /// <summary>Si el llamante pertenece a la organizacion, con cualquier papel.</summary>
    Task<bool> ReachesOrganizationAsync(Guid caller, Guid organizationId,
        CancellationToken ct = default);

    /// <summary>Si el llamante administra la organizacion entera.</summary>
    Task<bool> AdministersOrganizationAsync(Guid caller, Guid organizationId,
        CancellationToken ct = default);
}

/// <summary>
/// Las mismas preguntas en forma de guardia: o se cumple, o la peticion acaba
/// en 403.
/// </summary>
/// <remarks>
/// Estan como extensiones y no como metodos del puerto para que la
/// implementacion solo tenga que contestar si o no. Decidir que hacer con el
/// no es cosa de las membresias.
/// </remarks>
public static class AccessGuards
{
    /// <summary>
    /// El recurso es del llamante, o esta en un local que alcanza.
    /// </summary>
    /// <remarks>
    /// Es el caso corriente: casi todo agregado de la plataforma guarda a quien
    /// lo creo y el local al que pertenece. Se mira primero al dueño porque es
    /// gratis y resuelve la mayoria de las peticiones sin tocar la base.
    /// </remarks>
    public static async Task MustOwnOrReachSiteAsync(this IAccessPolicy policy, Guid caller,
        Guid ownerUserId, Guid siteId, CancellationToken ct = default)
    {
        if (ownerUserId == caller)
        {
            return;
        }
        if (siteId != Guid.Empty && await policy.ReachesSiteAsync(caller, siteId, ct))
        {
            return;
        }
        throw Negado();
    }

    /// <summary>
    /// Igual, para los modulos que guardan el identificador de usuario como
    /// texto.
    /// </summary>
    /// <remarks>
    /// Un texto que no es un identificador valido no puede ser del llamante, y
    /// tratarlo como si lo fuera seria abrir la puerta por donde no hay dato.
    /// </remarks>
    public static Task MustOwnOrReachSiteAsync(this IAccessPolicy policy, Guid caller,
        string? ownerUserId, Guid siteId, CancellationToken ct = default) =>
        policy.MustOwnOrReachSiteAsync(caller,
            Guid.TryParse(ownerUserId, out var dueno) ? dueno : Guid.Empty, siteId, ct);

    /// <summary>El recurso es del llamante, sin local al que recurrir.</summary>
    public static void MustOwn(Guid caller, Guid ownerUserId)
    {
        if (ownerUserId != caller)
        {
            throw Negado();
        }
    }

    /// <inheritdoc cref="MustOwn(Guid, Guid)"/>
    public static void MustOwn(Guid caller, string? ownerUserId) =>
        MustOwn(caller, Guid.TryParse(ownerUserId, out var dueno) ? dueno : Guid.Empty);

    public static async Task MustReachSiteAsync(this IAccessPolicy policy, Guid caller,
        Guid siteId, CancellationToken ct = default)
    {
        if (!await policy.ReachesSiteAsync(caller, siteId, ct))
        {
            throw Negado();
        }
    }

    public static async Task MustReachZoneAsync(this IAccessPolicy policy, Guid caller,
        Guid zoneId, CancellationToken ct = default)
    {
        if (!await policy.ReachesZoneAsync(caller, zoneId, ct))
        {
            throw Negado();
        }
    }

    public static async Task MustReachOrganizationAsync(this IAccessPolicy policy, Guid caller,
        Guid organizationId, CancellationToken ct = default)
    {
        if (!await policy.ReachesOrganizationAsync(caller, organizationId, ct))
        {
            throw Negado();
        }
    }

    public static async Task MustAdministerOrganizationAsync(this IAccessPolicy policy, Guid caller,
        Guid organizationId, CancellationToken ct = default)
    {
        if (!await policy.AdministersOrganizationAsync(caller, organizationId, ct))
        {
            throw AppException.Forbidden("this action requires an organization administrator");
        }
    }

    /// <summary>
    /// El mismo mensaje para todos los casos, a proposito.
    /// </summary>
    /// <remarks>
    /// Decir si el recurso existe, de quien es o por que falta el permiso seria
    /// contar algo de un tercero a quien no le corresponde preguntar.
    /// </remarks>
    private static AppException Negado() =>
        AppException.Forbidden("the resource does not belong to the authenticated user");
}
