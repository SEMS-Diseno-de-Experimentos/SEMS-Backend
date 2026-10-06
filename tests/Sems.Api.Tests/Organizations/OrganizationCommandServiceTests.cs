using NSubstitute;
using Sems.Api.Modules.Organizations.Application;
using Sems.Api.Modules.Organizations.Domain.Model;
using Sems.Api.Modules.Organizations.Domain.Repositories;
using Sems.Api.Shared.Errors;
using Xunit;

namespace Sems.Api.Tests.Organizations;

/// <summary>
/// Casos de uso de organizaciones, locales, zonas y vinculos, con los cuatro
/// repositorios simulados.
/// </summary>
public class OrganizationCommandServiceTests
{
    private readonly IOrganizationRepository _organizations = Substitute.For<IOrganizationRepository>();
    private readonly ISiteRepository _sites = Substitute.For<ISiteRepository>();
    private readonly IZoneRepository _zones = Substitute.For<IZoneRepository>();
    private readonly IMembershipRepository _memberships = Substitute.For<IMembershipRepository>();
    private readonly OrganizationCommandService _service;

    public OrganizationCommandServiceTests()
    {
        _organizations.SaveAsync(Arg.Any<Organization>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Organization>());
        _sites.SaveAsync(Arg.Any<Site>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Site>());
        _zones.SaveAsync(Arg.Any<Zone>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Zone>());
        _memberships.SaveAsync(Arg.Any<Membership>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Membership>());

        _service = new OrganizationCommandService(_organizations, _sites, _zones, _memberships);
    }

    private Organization KnownOrganization()
    {
        var organization = Organization.Register("Minimarket Los Andes S.A.C.", null, "20601234567",
            BusinessType.SUPERMARKET);
        _organizations.FindByIdAsync(organization.OrganizationId, Arg.Any<CancellationToken>())
            .Returns(organization);
        return organization;
    }

    private Site KnownSite(Guid organizationId)
    {
        var site = Site.Register(organizationId, "T-001", "Miraflores", null, null, null, 120m,
            TariffCategory.MT2);
        _sites.FindByIdAsync(site.SiteId, Arg.Any<CancellationToken>()).Returns(site);
        return site;
    }

    private static void AssertError(ErrorCode expected, AppException error) =>
        Assert.Equal(expected, error.Code);

    // ------------------------------------------------------------ organizacion

    [Fact]
    public async Task RegisterAsync_NewOrganization_SavesItAndMakesTheCreatorOrgAdmin()
    {
        var owner = Guid.NewGuid();

        var (organization, membership) = await _service.RegisterAsync("Minimarket Los Andes S.A.C.",
            "Los Andes", "20601234567", "SUPERMARKET", owner);

        await _organizations.Received(1).SaveAsync(organization, Arg.Any<CancellationToken>());
        await _memberships.Received(1).SaveAsync(
            Arg.Is<Membership>(m => m.UserId == owner && m.Role == MembershipRole.ORG_ADMIN
                                    && m.SiteId == null
                                    && m.OrganizationId == organization.OrganizationId),
            Arg.Any<CancellationToken>());
        Assert.Equal(MembershipRole.ORG_ADMIN, membership.Role);
    }

    [Fact]
    public async Task RegisterAsync_TaxIdAlreadyRegistered_ThrowsConflictAndSavesNothing()
    {
        _organizations.ExistsByTaxIdAsync("20609876543", Arg.Any<CancellationToken>()).Returns(true);

        var error = await Assert.ThrowsAsync<AppException>(() => _service.RegisterAsync(
            "Copycat S.A.C.", null, "20609876543", "SUPERMARKET", Guid.NewGuid()));

        AssertError(ErrorCode.CONFLICT, error);
        await _organizations.DidNotReceive().SaveAsync(Arg.Any<Organization>(), Arg.Any<CancellationToken>());
        await _memberships.DidNotReceive().SaveAsync(Arg.Any<Membership>(), Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------ locales

    [Fact]
    public async Task RegisterSiteAsync_SiteCodeRepeatedInTheOrganization_ThrowsConflict()
    {
        var organization = KnownOrganization();
        _sites.ExistsBySiteCodeAsync(organization.OrganizationId, "T-001", Arg.Any<CancellationToken>())
            .Returns(true);

        var error = await Assert.ThrowsAsync<AppException>(() => _service.RegisterSiteAsync(
            organization.OrganizationId, "t-001", "Miraflores", null, null, null, 120m, "MT2"));

        AssertError(ErrorCode.CONFLICT, error);
        await _sites.DidNotReceive().SaveAsync(Arg.Any<Site>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterSiteAsync_UnknownOrganization_ThrowsNotFound()
    {
        var error = await Assert.ThrowsAsync<AppException>(() => _service.RegisterSiteAsync(
            Guid.NewGuid(), "T-001", "Miraflores", null, null, null, 120m, "MT2"));

        AssertError(ErrorCode.NOT_FOUND, error);
    }

    [Fact]
    public async Task RegisterSiteAsync_SuspendedOrganization_ThrowsConflict()
    {
        var organization = KnownOrganization();
        organization.Suspend();

        var error = await Assert.ThrowsAsync<AppException>(() => _service.RegisterSiteAsync(
            organization.OrganizationId, "T-001", "Miraflores", null, null, null, 120m, "MT2"));

        AssertError(ErrorCode.CONFLICT, error);
    }

    [Fact]
    public async Task RegisterSiteAsync_ValidSite_IsSavedInTheOrganization()
    {
        var organization = KnownOrganization();

        var site = await _service.RegisterSiteAsync(organization.OrganizationId, "t-002", "San Isidro",
            "Av. Camino Real 456", "San Isidro", 380m, 250m, "mt2");

        Assert.Equal(organization.OrganizationId, site.OrganizationId);
        Assert.Equal("T-002", site.SiteCode);
        Assert.Equal(TariffCategory.MT2, site.TariffCategory);
        await _sites.Received(1).SaveAsync(site, Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------- zonas

    [Fact]
    public async Task RegisterZoneAsync_ArchivedSite_ThrowsConflict()
    {
        var site = KnownSite(Guid.NewGuid());
        site.Archive();

        var error = await Assert.ThrowsAsync<AppException>(() =>
            _service.RegisterZoneAsync(site.SiteId, "Cold rooms", "COLD_STORAGE", null));

        AssertError(ErrorCode.CONFLICT, error);
        await _zones.DidNotReceive().SaveAsync(Arg.Any<Zone>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterZoneAsync_UnknownSite_ThrowsNotFound()
    {
        var error = await Assert.ThrowsAsync<AppException>(() =>
            _service.RegisterZoneAsync(Guid.NewGuid(), "Cold rooms", "COLD_STORAGE", null));

        AssertError(ErrorCode.NOT_FOUND, error);
    }

    // ---------------------------------------------------------------- vinculos

    [Fact]
    public async Task GrantMembershipAsync_SupervisorOnASiteOfAnotherOrganization_ThrowsValidationError()
    {
        var organization = KnownOrganization();
        var foreignSite = KnownSite(Guid.NewGuid());

        var error = await Assert.ThrowsAsync<AppException>(() => _service.GrantMembershipAsync(
            organization.OrganizationId, Guid.NewGuid(), "SUPERVISOR", foreignSite.SiteId));

        AssertError(ErrorCode.VALIDATION_ERROR, error);
        await _memberships.DidNotReceive().SaveAsync(Arg.Any<Membership>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeMembershipAsync_LastAdministrator_ThrowsConflict()
    {
        var organizationId = Guid.NewGuid();
        var admin = Membership.Grant(organizationId, Guid.NewGuid(), MembershipRole.ORG_ADMIN, null);
        _memberships.FindByIdAsync(admin.MembershipId, Arg.Any<CancellationToken>()).Returns(admin);
        _memberships.FindByOrganizationIdAsync(organizationId, Arg.Any<CancellationToken>())
            .Returns(new List<Membership> { admin });

        var error = await Assert.ThrowsAsync<AppException>(() =>
            _service.RevokeMembershipAsync(admin.MembershipId));

        AssertError(ErrorCode.CONFLICT, error);
        Assert.True(admin.IsActive);
    }

    [Fact]
    public async Task RevokeMembershipAsync_OneOfTwoAdministrators_RevokesIt()
    {
        var organizationId = Guid.NewGuid();
        var first = Membership.Grant(organizationId, Guid.NewGuid(), MembershipRole.ORG_ADMIN, null);
        var second = Membership.Grant(organizationId, Guid.NewGuid(), MembershipRole.ORG_ADMIN, null);
        _memberships.FindByIdAsync(second.MembershipId, Arg.Any<CancellationToken>()).Returns(second);
        _memberships.FindByOrganizationIdAsync(organizationId, Arg.Any<CancellationToken>())
            .Returns(new List<Membership> { first, second });

        await _service.RevokeMembershipAsync(second.MembershipId);

        Assert.False(second.IsActive);
        await _memberships.Received(1).SaveAsync(second, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GrantMembershipAsync_PersonWhoseAccessWasRevoked_ReinstatesTheSameMembership()
    {
        var organization = KnownOrganization();
        var userId = Guid.NewGuid();
        var revoked = Membership.Grant(organization.OrganizationId, userId, MembershipRole.OPERATOR, null);
        revoked.Revoke();
        _memberships.FindByOrganizationAndUserAsync(organization.OrganizationId, userId,
            Arg.Any<CancellationToken>()).Returns(revoked);

        var granted = await _service.GrantMembershipAsync(organization.OrganizationId, userId,
            "OPERATOR", null);

        Assert.Same(revoked, granted);
        Assert.True(granted.IsActive);
        await _memberships.Received(1).SaveAsync(revoked, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GrantMembershipAsync_PersonWithActiveAccess_ChangesTheirRole()
    {
        var organization = KnownOrganization();
        var site = KnownSite(organization.OrganizationId);
        var userId = Guid.NewGuid();
        var current = Membership.Grant(organization.OrganizationId, userId, MembershipRole.OPERATOR, null);
        _memberships.FindByOrganizationAndUserAsync(organization.OrganizationId, userId,
            Arg.Any<CancellationToken>()).Returns(current);

        var granted = await _service.GrantMembershipAsync(organization.OrganizationId, userId,
            "SUPERVISOR", site.SiteId);

        Assert.Same(current, granted);
        Assert.Equal(MembershipRole.SUPERVISOR, granted.Role);
        Assert.Equal(site.SiteId, granted.SiteId);
    }
}
