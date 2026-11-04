using Microsoft.EntityFrameworkCore;
using Sems.Api.Modules.Energy.Domain.Services;
using Sems.Api.Modules.Organizations.Domain.Model;
using Sems.Api.Shared.Persistence;

namespace Sems.Api.Modules.Energy.Infrastructure;

/// <summary>
/// Resuelve <see cref="ISiteCatalog"/> contra las tablas de organizaciones.
/// </summary>
/// <remarks>
/// La dependencia hacia el otro modulo vive aqui, en infraestructura, que es la
/// capa a la que le toca conocer el mundo exterior. El dominio y la aplicacion
/// de energia siguen sin saber que existe una organizacion.
/// </remarks>
public sealed class OrganizationsSiteCatalog : ISiteCatalog
{
    private readonly SemsDbContext _db;

    public OrganizationsSiteCatalog(SemsDbContext db) => _db = db;

    public Task<bool> SiteExistsAsync(Guid siteId, CancellationToken ct = default) =>
        _db.Set<Site>().AnyAsync(s => s.SiteId == siteId, ct);

    public Task<bool> SiteIsActiveAsync(Guid siteId, CancellationToken ct = default) =>
        _db.Set<Site>().AnyAsync(s => s.SiteId == siteId && s.Status == OrgStatus.ACTIVE, ct);

    public Task<bool> ZoneBelongsToSiteAsync(Guid zoneId, Guid siteId, CancellationToken ct = default) =>
        _db.Set<Zone>().AnyAsync(
            z => z.ZoneId == zoneId && z.SiteId == siteId && z.Status == OrgStatus.ACTIVE, ct);

    public async Task<ISiteCatalog.SiteSummary?> SiteSummaryAsync(Guid siteId,
        CancellationToken ct = default)
    {
        var local = await _db.Set<Site>()
            .FirstOrDefaultAsync(s => s.SiteId == siteId && s.Status == OrgStatus.ACTIVE, ct);
        return local is null ? null : Resumen(local);
    }

    public async Task<List<ISiteCatalog.SiteSummary>> ActiveSitesOfAsync(Guid organizationId,
        CancellationToken ct = default)
    {
        var locales = await _db.Set<Site>()
            .Where(s => s.OrganizationId == organizationId && s.Status == OrgStatus.ACTIVE)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);
        return locales.Select(Resumen).ToList();
    }

    public async Task<List<ISiteCatalog.ZoneSummary>> ActiveZonesOfAsync(Guid siteId,
        CancellationToken ct = default)
    {
        var zonas = await _db.Set<Zone>()
            .Where(z => z.SiteId == siteId && z.Status == OrgStatus.ACTIVE)
            .ToListAsync(ct);
        return zonas
            .OrderBy(z => z.Name, StringComparer.OrdinalIgnoreCase)
            .Select(z => new ISiteCatalog.ZoneSummary(z.ZoneId, z.Name, z.ZoneType.ToString()))
            .ToList();
    }

    private static ISiteCatalog.SiteSummary Resumen(Site local) =>
        new(local.SiteId, local.SiteCode, local.Name, local.ContractedPowerKw,
            local.TariffCategory.ToString(), local.ExcludesSundaysFromPeak);
}
