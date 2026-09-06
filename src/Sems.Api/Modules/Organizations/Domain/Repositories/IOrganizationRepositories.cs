using Sems.Api.Modules.Organizations.Domain.Model;

namespace Sems.Api.Modules.Organizations.Domain.Repositories;

/// <summary>Puertos de salida del modulo de organizaciones.</summary>
public interface IOrganizationRepository
{
    Task<Organization> SaveAsync(Organization organization, CancellationToken ct = default);

    Task<Organization?> FindByIdAsync(Guid organizationId, CancellationToken ct = default);

    Task<Organization?> FindByTaxIdAsync(string taxId, CancellationToken ct = default);

    Task<bool> ExistsByTaxIdAsync(string taxId, CancellationToken ct = default);
}

public interface ISiteRepository
{
    Task<Site> SaveAsync(Site site, CancellationToken ct = default);

    Task<Site?> FindByIdAsync(Guid siteId, CancellationToken ct = default);

    /// <summary>Locales vigentes de la organizacion. Los archivados no salen.</summary>
    Task<List<Site>> FindByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default);

    Task<bool> ExistsBySiteCodeAsync(Guid organizationId, string siteCode, CancellationToken ct = default);
}

public interface IZoneRepository
{
    Task<Zone> SaveAsync(Zone zone, CancellationToken ct = default);

    Task<Zone?> FindByIdAsync(Guid zoneId, CancellationToken ct = default);

    /// <summary>Zonas vigentes del local. Las archivadas no salen.</summary>
    Task<List<Zone>> FindBySiteIdAsync(Guid siteId, CancellationToken ct = default);
}

public interface IMembershipRepository
{
    Task<Membership> SaveAsync(Membership membership, CancellationToken ct = default);

    Task<Membership?> FindByIdAsync(Guid membershipId, CancellationToken ct = default);

    Task<List<Membership>> FindByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>Vinculos vigentes de una persona. Puede pertenecer a varias organizaciones.</summary>
    Task<List<Membership>> FindByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task<Membership?> FindByOrganizationAndUserAsync(Guid organizationId, Guid userId,
        CancellationToken ct = default);
}
