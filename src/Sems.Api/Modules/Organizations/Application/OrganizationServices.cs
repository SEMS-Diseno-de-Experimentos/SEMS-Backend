using Sems.Api.Modules.Organizations.Domain.Model;
using Sems.Api.Modules.Organizations.Domain.Repositories;
using Sems.Api.Shared.Errors;

namespace Sems.Api.Modules.Organizations.Application;

/// <summary>
/// Casos de uso que modifican organizaciones, locales, zonas y vinculos.
/// </summary>
public sealed class OrganizationCommandService
{
    private readonly IOrganizationRepository _organizations;
    private readonly ISiteRepository _sites;
    private readonly IZoneRepository _zones;
    private readonly IMembershipRepository _memberships;

    public OrganizationCommandService(IOrganizationRepository organizations, ISiteRepository sites,
        IZoneRepository zones, IMembershipRepository memberships)
    {
        _organizations = organizations;
        _sites = sites;
        _zones = zones;
        _memberships = memberships;
    }

    /// <summary>
    /// Da de alta una organizacion y deja a quien la crea como administrador.
    /// </summary>
    /// <remarks>
    /// Las dos cosas van juntas a proposito. Una organizacion sin ningun
    /// administrador no la puede gestionar nadie: no habria quien diera de alta
    /// el primer local ni quien invitara al primer supervisor.
    /// </remarks>
    public async Task<(Organization Organizacion, Membership Vinculo)> RegisterAsync(
        string? legalName, string? tradeName, string? taxId, string? businessType,
        Guid ownerUserId, CancellationToken ct = default)
    {
        var ruc = (taxId ?? string.Empty).Trim();
        if (ruc.Length > 0 && await _organizations.ExistsByTaxIdAsync(ruc, ct))
        {
            throw AppException.Conflict("an organization with that tax_id already exists");
        }

        var organizacion = Organization.Register(legalName, tradeName, taxId,
            OrganizationEnums.ToBusinessType(businessType));
        await _organizations.SaveAsync(organizacion, ct);

        var vinculo = Membership.Grant(organizacion.OrganizationId, ownerUserId,
            MembershipRole.ORG_ADMIN, null);
        await _memberships.SaveAsync(vinculo, ct);

        return (organizacion, vinculo);
    }

    public async Task<Organization> UpdateAsync(Guid organizationId, string? legalName,
        string? tradeName, string? businessType, CancellationToken ct = default)
    {
        var organizacion = await _organizations.FindByIdAsync(organizationId, ct)
                           ?? throw AppException.NotFound("organization not found");

        organizacion.UpdateDetails(legalName, tradeName,
            OrganizationEnums.ToBusinessType(businessType));
        return await _organizations.SaveAsync(organizacion, ct);
    }

    public async Task<Site> RegisterSiteAsync(Guid organizationId, string? siteCode, string? name,
        string? address, string? district, decimal? floorAreaM2, decimal contractedPowerKw,
        string? tariffCategory, CancellationToken ct = default)
    {
        var organizacion = await _organizations.FindByIdAsync(organizationId, ct)
                           ?? throw AppException.NotFound("organization not found");

        if (!organizacion.IsActive)
        {
            throw AppException.Conflict("the organization is not active");
        }

        var codigo = (siteCode ?? string.Empty).Trim().ToUpperInvariant();
        if (codigo.Length > 0 && await _sites.ExistsBySiteCodeAsync(organizationId, codigo, ct))
        {
            throw AppException.Conflict("a site with that site_code already exists in this organization");
        }

        var local = Site.Register(organizationId, siteCode, name, address, district, floorAreaM2,
            contractedPowerKw, OrganizationEnums.ToTariffCategory(tariffCategory));
        return await _sites.SaveAsync(local, ct);
    }

    public async Task<Site> UpdateSiteAsync(Guid siteId, string? name, string? address,
        string? district, decimal? floorAreaM2, decimal contractedPowerKw, string? tariffCategory,
        CancellationToken ct = default)
    {
        var local = await _sites.FindByIdAsync(siteId, ct)
                    ?? throw AppException.NotFound("site not found");

        local.UpdateDetails(name, address, district, floorAreaM2, contractedPowerKw,
            OrganizationEnums.ToTariffCategory(tariffCategory));
        return await _sites.SaveAsync(local, ct);
    }

    public async Task ArchiveSiteAsync(Guid siteId, CancellationToken ct = default)
    {
        var local = await _sites.FindByIdAsync(siteId, ct)
                    ?? throw AppException.NotFound("site not found");

        local.Archive();
        await _sites.SaveAsync(local, ct);
    }

    public async Task<Zone> RegisterZoneAsync(Guid siteId, string? name, string? zoneType,
        bool? operatesOffHours, CancellationToken ct = default)
    {
        var local = await _sites.FindByIdAsync(siteId, ct)
                    ?? throw AppException.NotFound("site not found");

        if (!local.IsActive)
        {
            throw AppException.Conflict("the site is not active");
        }

        var zona = Zone.Register(siteId, name, OrganizationEnums.ToZoneType(zoneType),
            operatesOffHours);
        return await _zones.SaveAsync(zona, ct);
    }

    public async Task<Zone> UpdateZoneAsync(Guid zoneId, string? name, string? zoneType,
        bool operatesOffHours, CancellationToken ct = default)
    {
        var zona = await _zones.FindByIdAsync(zoneId, ct)
                   ?? throw AppException.NotFound("zone not found");

        zona.UpdateDetails(name, OrganizationEnums.ToZoneType(zoneType), operatesOffHours);
        return await _zones.SaveAsync(zona, ct);
    }

    public async Task ArchiveZoneAsync(Guid zoneId, CancellationToken ct = default)
    {
        var zona = await _zones.FindByIdAsync(zoneId, ct)
                   ?? throw AppException.NotFound("zone not found");

        zona.Archive();
        await _zones.SaveAsync(zona, ct);
    }

    /// <summary>Da acceso a una persona, o cambia el que ya tenia.</summary>
    public async Task<Membership> GrantMembershipAsync(Guid organizationId, Guid userId,
        string? role, Guid? siteId, CancellationToken ct = default)
    {
        _ = await _organizations.FindByIdAsync(organizationId, ct)
            ?? throw AppException.NotFound("organization not found");

        if (siteId is not null)
        {
            var local = await _sites.FindByIdAsync(siteId.Value, ct)
                        ?? throw AppException.NotFound("site not found");

            // Sin esta comprobacion se podria dar a alguien acceso de supervisor
            // sobre el local de otra empresa pasando el identificador a mano.
            if (local.OrganizationId != organizationId)
            {
                throw AppException.Validation("the site does not belong to this organization");
            }
        }

        var papel = OrganizationEnums.ToMembershipRole(role);
        var existente = await _memberships.FindByOrganizationAndUserAsync(organizationId, userId, ct);

        if (existente is not null)
        {
            existente.ChangeRole(papel, siteId);
            return await _memberships.SaveAsync(existente, ct);
        }

        var vinculo = Membership.Grant(organizationId, userId, papel, siteId);
        return await _memberships.SaveAsync(vinculo, ct);
    }

    public async Task RevokeMembershipAsync(Guid membershipId, CancellationToken ct = default)
    {
        var vinculo = await _memberships.FindByIdAsync(membershipId, ct)
                      ?? throw AppException.NotFound("membership not found");

        // Dejar una organizacion sin administradores la vuelve ingobernable: no
        // quedaria nadie que pueda dar de alta locales ni devolver el acceso.
        if (vinculo.Role == MembershipRole.ORG_ADMIN)
        {
            var vinculos = await _memberships.FindByOrganizationIdAsync(vinculo.OrganizationId, ct);
            var administradores = vinculos.Count(m => m.Role == MembershipRole.ORG_ADMIN);
            if (administradores <= 1)
            {
                throw AppException.Conflict("the organization would be left without an administrator");
            }
        }

        vinculo.Revoke();
        await _memberships.SaveAsync(vinculo, ct);
    }
}

/// <summary>Consultas del modulo de organizaciones.</summary>
public sealed class OrganizationQueryService
{
    private readonly IOrganizationRepository _organizations;
    private readonly ISiteRepository _sites;
    private readonly IZoneRepository _zones;
    private readonly IMembershipRepository _memberships;

    public OrganizationQueryService(IOrganizationRepository organizations, ISiteRepository sites,
        IZoneRepository zones, IMembershipRepository memberships)
    {
        _organizations = organizations;
        _sites = sites;
        _zones = zones;
        _memberships = memberships;
    }

    public async Task<Organization> GetAsync(Guid organizationId, CancellationToken ct = default) =>
        await _organizations.FindByIdAsync(organizationId, ct)
        ?? throw AppException.NotFound("organization not found");

    public Task<List<Site>> ListSitesAsync(Guid organizationId, CancellationToken ct = default) =>
        _sites.FindByOrganizationIdAsync(organizationId, ct);

    public async Task<Site> GetSiteAsync(Guid siteId, CancellationToken ct = default) =>
        await _sites.FindByIdAsync(siteId, ct) ?? throw AppException.NotFound("site not found");

    public Task<List<Zone>> ListZonesAsync(Guid siteId, CancellationToken ct = default) =>
        _zones.FindBySiteIdAsync(siteId, ct);

    public Task<List<Membership>> ListMembershipsAsync(Guid organizationId,
        CancellationToken ct = default) =>
        _memberships.FindByOrganizationIdAsync(organizationId, ct);

    /// <summary>Organizaciones a las que pertenece una persona.</summary>
    public async Task<List<(Organization Organizacion, Membership Vinculo)>> ListMineAsync(
        Guid userId, CancellationToken ct = default)
    {
        var vinculos = await _memberships.FindByUserIdAsync(userId, ct);
        var resultado = new List<(Organization, Membership)>();

        foreach (var vinculo in vinculos)
        {
            var organizacion = await _organizations.FindByIdAsync(vinculo.OrganizationId, ct);
            if (organizacion is not null)
            {
                resultado.Add((organizacion, vinculo));
            }
        }

        return resultado;
    }
}
