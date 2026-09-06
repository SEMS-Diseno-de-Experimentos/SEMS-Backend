namespace Sems.Api.Modules.Devices.Domain.Services;

/// <summary>
/// Lo unico que el modulo de dispositivos necesita saber sobre locales y zonas.
/// </summary>
/// <remarks>
/// <para>Es un puerto y no una llamada directa al modulo de organizaciones a
/// proposito. Dispositivos no tiene por que conocer que es una organizacion, ni
/// una categoria tarifaria, ni un vinculo de acceso: solo necesita responder a
/// dos preguntas antes de dar de alta un equipo.</para>
///
/// <para>Declarar aqui esa frontera es lo que evita que un monolito modular se
/// convierta en una maranha donde cualquier modulo llama a cualquier otro.</para>
/// </remarks>
public interface ISiteDirectory
{
    /// <summary>Si el local existe y esta vigente.</summary>
    Task<bool> SiteIsActiveAsync(Guid siteId, CancellationToken ct = default);

    /// <summary>Si la zona existe, esta vigente y pertenece a ese local.</summary>
    Task<bool> ZoneBelongsToSiteAsync(Guid zoneId, Guid siteId, CancellationToken ct = default);
}
