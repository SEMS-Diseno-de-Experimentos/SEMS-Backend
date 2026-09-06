using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sems.Api.Modules.Organizations.Domain.Model;
using Sems.Api.Modules.Organizations.Domain.Repositories;
using Sems.Api.Shared.Persistence;

namespace Sems.Api.Modules.Organizations.Infrastructure;

/// <summary>
/// Mapeo a tablas del modulo de organizaciones. Prefijo <c>og_</c>.
/// </summary>
public sealed class OrganizationConfig : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("og_organizations");
        builder.HasKey(o => o.OrganizationId);
        builder.Property(o => o.LegalName).HasMaxLength(200).IsRequired();
        builder.Property(o => o.TradeName).HasMaxLength(200);
        builder.Property(o => o.TaxId).HasMaxLength(11).IsRequired();
        builder.Property(o => o.BusinessType).HasConversion<string>().HasMaxLength(30);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(o => o.TaxId).IsUnique();
    }
}

public sealed class SiteConfig : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> builder)
    {
        builder.ToTable("og_sites");
        builder.HasKey(s => s.SiteId);
        builder.Property(s => s.SiteCode).HasMaxLength(40).IsRequired();
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Address).HasMaxLength(300);
        builder.Property(s => s.District).HasMaxLength(120);
        builder.Property(s => s.FloorAreaM2).HasPrecision(12, 2);
        builder.Property(s => s.ContractedPowerKw).HasPrecision(12, 2);
        builder.Property(s => s.TariffCategory).HasConversion<string>().HasMaxLength(10);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(s => s.OrganizationId);
        // El codigo de local se repite entre cadenas distintas (dos empresas
        // pueden llamar "T-001" a su primera tienda), pero no dentro de la misma.
        builder.HasIndex(s => new { s.OrganizationId, s.SiteCode }).IsUnique();
    }
}

public sealed class ZoneConfig : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> builder)
    {
        builder.ToTable("og_zones");
        builder.HasKey(z => z.ZoneId);
        builder.Property(z => z.Name).HasMaxLength(160).IsRequired();
        builder.Property(z => z.ZoneType).HasConversion<string>().HasMaxLength(30);
        builder.Property(z => z.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(z => z.SiteId);
    }
}

public sealed class MembershipConfig : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("og_memberships");
        builder.HasKey(m => m.MembershipId);
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(m => m.UserId);
        builder.HasIndex(m => m.OrganizationId);
        // Una persona tiene un solo papel en cada organizacion. Si necesita
        // acceso a dos locales con papel de supervisor, se le da acceso de
        // organizacion, no dos vinculos que luego habria que resolver por
        // prioridad.
        builder.HasIndex(m => new { m.OrganizationId, m.UserId }).IsUnique();
    }
}

public sealed class OrganizationRepository : IOrganizationRepository
{
    private readonly SemsDbContext _db;

    public OrganizationRepository(SemsDbContext db) => _db = db;

    public async Task<Organization> SaveAsync(Organization organization, CancellationToken ct = default)
    {
        var existe = await _db.Set<Organization>()
            .AnyAsync(o => o.OrganizationId == organization.OrganizationId, ct);
        if (existe)
        {
            _db.Set<Organization>().Update(organization);
        }
        else
        {
            await _db.Set<Organization>().AddAsync(organization, ct);
        }
        await _db.SaveChangesAsync(ct);
        return organization;
    }

    public Task<Organization?> FindByIdAsync(Guid organizationId, CancellationToken ct = default) =>
        _db.Set<Organization>().FirstOrDefaultAsync(o => o.OrganizationId == organizationId, ct);

    public Task<Organization?> FindByTaxIdAsync(string taxId, CancellationToken ct = default) =>
        _db.Set<Organization>().FirstOrDefaultAsync(o => o.TaxId == taxId, ct);

    public Task<bool> ExistsByTaxIdAsync(string taxId, CancellationToken ct = default) =>
        _db.Set<Organization>().AnyAsync(o => o.TaxId == taxId, ct);
}

public sealed class SiteRepository : ISiteRepository
{
    private readonly SemsDbContext _db;

    public SiteRepository(SemsDbContext db) => _db = db;

    public async Task<Site> SaveAsync(Site site, CancellationToken ct = default)
    {
        var existe = await _db.Set<Site>().AnyAsync(s => s.SiteId == site.SiteId, ct);
        if (existe)
        {
            _db.Set<Site>().Update(site);
        }
        else
        {
            await _db.Set<Site>().AddAsync(site, ct);
        }
        await _db.SaveChangesAsync(ct);
        return site;
    }

    public Task<Site?> FindByIdAsync(Guid siteId, CancellationToken ct = default) =>
        _db.Set<Site>().FirstOrDefaultAsync(s => s.SiteId == siteId, ct);

    // Los archivados quedan en la base porque de ellos cuelga el historico de
    // consumo, pero no se listan: si salieran, un local cerrado seguiria
    // apareciendo en el panel y contando para las comparaciones entre tiendas.
    public Task<List<Site>> FindByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default) =>
        _db.Set<Site>()
            .Where(s => s.OrganizationId == organizationId && s.Status != OrgStatus.ARCHIVED)
            .OrderBy(s => s.SiteCode)
            .ToListAsync(ct);

    public Task<bool> ExistsBySiteCodeAsync(Guid organizationId, string siteCode,
        CancellationToken ct = default) =>
        _db.Set<Site>().AnyAsync(s => s.OrganizationId == organizationId && s.SiteCode == siteCode, ct);
}

public sealed class ZoneRepository : IZoneRepository
{
    private readonly SemsDbContext _db;

    public ZoneRepository(SemsDbContext db) => _db = db;

    public async Task<Zone> SaveAsync(Zone zone, CancellationToken ct = default)
    {
        var existe = await _db.Set<Zone>().AnyAsync(z => z.ZoneId == zone.ZoneId, ct);
        if (existe)
        {
            _db.Set<Zone>().Update(zone);
        }
        else
        {
            await _db.Set<Zone>().AddAsync(zone, ct);
        }
        await _db.SaveChangesAsync(ct);
        return zone;
    }

    public Task<Zone?> FindByIdAsync(Guid zoneId, CancellationToken ct = default) =>
        _db.Set<Zone>().FirstOrDefaultAsync(z => z.ZoneId == zoneId, ct);

    public Task<List<Zone>> FindBySiteIdAsync(Guid siteId, CancellationToken ct = default) =>
        _db.Set<Zone>()
            .Where(z => z.SiteId == siteId && z.Status != OrgStatus.ARCHIVED)
            .OrderBy(z => z.Name)
            .ToListAsync(ct);
}

public sealed class MembershipRepository : IMembershipRepository
{
    private readonly SemsDbContext _db;

    public MembershipRepository(SemsDbContext db) => _db = db;

    public async Task<Membership> SaveAsync(Membership membership, CancellationToken ct = default)
    {
        var existe = await _db.Set<Membership>()
            .AnyAsync(m => m.MembershipId == membership.MembershipId, ct);
        if (existe)
        {
            _db.Set<Membership>().Update(membership);
        }
        else
        {
            await _db.Set<Membership>().AddAsync(membership, ct);
        }
        await _db.SaveChangesAsync(ct);
        return membership;
    }

    public Task<Membership?> FindByIdAsync(Guid membershipId, CancellationToken ct = default) =>
        _db.Set<Membership>().FirstOrDefaultAsync(m => m.MembershipId == membershipId, ct);

    public Task<List<Membership>> FindByOrganizationIdAsync(Guid organizationId,
        CancellationToken ct = default) =>
        _db.Set<Membership>()
            .Where(m => m.OrganizationId == organizationId && m.Status != OrgStatus.ARCHIVED)
            .ToListAsync(ct);

    public Task<List<Membership>> FindByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.Set<Membership>()
            .Where(m => m.UserId == userId && m.Status != OrgStatus.ARCHIVED)
            .ToListAsync(ct);

    public Task<Membership?> FindByOrganizationAndUserAsync(Guid organizationId, Guid userId,
        CancellationToken ct = default) =>
        _db.Set<Membership>().FirstOrDefaultAsync(
            m => m.OrganizationId == organizationId && m.UserId == userId
                 && m.Status != OrgStatus.ARCHIVED, ct);
}
