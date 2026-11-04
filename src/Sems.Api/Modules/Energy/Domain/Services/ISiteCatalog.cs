namespace Sems.Api.Modules.Energy.Domain.Services;

/// <summary>
/// Lo que el modulo de energia necesita saber sobre locales y zonas.
/// </summary>
/// <remarks>
/// <para>Es un puerto declarado por el consumidor, igual que el de
/// dispositivos: energia no tiene por que conocer que es una organizacion ni
/// un vinculo de acceso. Solo necesita saber si el local existe y esta
/// vigente, si una zona es de ese local, y —para poner precio al consumo— con
/// que tarifa se factura.</para>
/// </remarks>
public interface ISiteCatalog
{
    /// <summary>Datos del local que energia necesita para valorizar su consumo.</summary>
    /// <remarks>
    /// La categoria tarifaria viaja como texto porque energia no conoce el enum
    /// de organizaciones; es quien lo recibe el que lo traduce.
    /// </remarks>
    public sealed record SiteSummary(Guid SiteId, string SiteCode, string Name,
        decimal ContractedPowerKw, string? TariffCategory, bool ExcludesSundaysFromPeak);

    /// <summary>Zona vigente de un local.</summary>
    public sealed record ZoneSummary(Guid ZoneId, string Name, string ZoneType);

    /// <summary>Si el local existe, este vigente o no.</summary>
    Task<bool> SiteExistsAsync(Guid siteId, CancellationToken ct = default);

    /// <summary>Si el local existe y esta vigente.</summary>
    Task<bool> SiteIsActiveAsync(Guid siteId, CancellationToken ct = default);

    /// <summary>Si la zona existe, esta vigente y pertenece a ese local.</summary>
    Task<bool> ZoneBelongsToSiteAsync(Guid zoneId, Guid siteId, CancellationToken ct = default);

    /// <summary>Datos del local, o null si no existe o esta archivado.</summary>
    Task<SiteSummary?> SiteSummaryAsync(Guid siteId, CancellationToken ct = default);

    /// <summary>Locales vigentes de la organizacion, por orden de registro.</summary>
    Task<List<SiteSummary>> ActiveSitesOfAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>Zonas vigentes del local, por nombre.</summary>
    Task<List<ZoneSummary>> ActiveZonesOfAsync(Guid siteId, CancellationToken ct = default);
}
