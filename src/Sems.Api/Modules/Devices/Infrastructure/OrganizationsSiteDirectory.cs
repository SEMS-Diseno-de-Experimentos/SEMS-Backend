using Microsoft.EntityFrameworkCore;
using Sems.Api.Modules.Devices.Domain.Services;
using Sems.Api.Modules.Organizations.Domain.Model;
using Sems.Api.Shared.Persistence;

namespace Sems.Api.Modules.Devices.Infrastructure;

/// <summary>
/// Resuelve <see cref="ISiteDirectory"/> contra las tablas del modulo de
/// organizaciones.
/// </summary>
/// <remarks>
/// La dependencia hacia el otro modulo vive aqui, en infraestructura, que es la
/// capa a la que le corresponde conocer el mundo exterior. El dominio y la capa
/// de aplicacion de dispositivos siguen sin saber que existe una organizacion.
/// </remarks>
public sealed class OrganizationsSiteDirectory : ISiteDirectory
{
    private readonly SemsDbContext _db;

    public OrganizationsSiteDirectory(SemsDbContext db) => _db = db;

    public Task<bool> SiteIsActiveAsync(Guid siteId, CancellationToken ct = default) =>
        _db.Set<Site>().AnyAsync(s => s.SiteId == siteId && s.Status == OrgStatus.ACTIVE, ct);

    public Task<bool> ZoneBelongsToSiteAsync(Guid zoneId, Guid siteId, CancellationToken ct = default) =>
        _db.Set<Zone>().AnyAsync(
            z => z.ZoneId == zoneId && z.SiteId == siteId && z.Status == OrgStatus.ACTIVE, ct);
}
