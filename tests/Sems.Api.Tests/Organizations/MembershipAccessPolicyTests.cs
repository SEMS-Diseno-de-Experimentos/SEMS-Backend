using Xunit;
using NSubstitute;
using Sems.Api.Modules.Organizations.Domain.Model;
using Sems.Api.Modules.Organizations.Domain.Repositories;
using Sems.Api.Modules.Organizations.Infrastructure;
using Sems.Api.Shared.Errors;
using Sems.Api.Shared.Security;

namespace Sems.Api.Tests.Organizations;

/// <summary>
/// El alcance de un usuario sobre locales y organizaciones.
/// </summary>
/// <remarks>
/// Es la pieza de la que cuelga la autorizacion de todo lo que no es "mio":
/// si contesta de mas, se filtra el dato del local de al lado; si contesta de
/// menos, el jefe de tienda se queda sin ver el suyo. Las dos equivocaciones
/// se prueban aqui.
/// </remarks>
public class MembershipAccessPolicyTests
{
    private readonly IMembershipRepository _memberships = Substitute.For<IMembershipRepository>();
    private readonly ISiteRepository _sites = Substitute.For<ISiteRepository>();
    private readonly IZoneRepository _zones = Substitute.For<IZoneRepository>();

    private readonly Guid _organizacion = Guid.NewGuid();
    private readonly Guid _otraOrganizacion = Guid.NewGuid();

    public MembershipAccessPolicyTests() =>
        // Por defecto, nadie tiene vinculos. El repositorio real devuelve una
        // lista vacia y no nulo, asi que el doble tiene que hacer lo mismo o la
        // prueba mediria algo que no pasa en produccion.
        _memberships.FindByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Membership>());

    private MembershipAccessPolicy Politica() =>
        new(_memberships, _sites, _zones);

    private Site LocalDe(Guid organizationId) =>
        Site.Register(organizationId, $"S-{Guid.NewGuid():N}"[..8], "Tienda", "Av. Larco 100",
            "Miraflores", 180m, 240m, TariffCategory.MT2);

    private void ConVinculos(Guid usuario, params Membership[] vinculos) =>
        _memberships.FindByUserIdAsync(usuario, Arg.Any<CancellationToken>())
            .Returns(vinculos.ToList());

    [Fact]
    public async Task AnOrgWideMembership_ReachesEverySiteOfThatOrganization()
    {
        var jefe = Guid.NewGuid();
        var local = LocalDe(_organizacion);
        _sites.FindByIdAsync(local.SiteId, Arg.Any<CancellationToken>()).Returns(local);
        ConVinculos(jefe, Membership.Grant(_organizacion, jefe, MembershipRole.ORG_ADMIN, null));

        Assert.True(await Politica().ReachesSiteAsync(jefe, local.SiteId));
        Assert.True(await Politica().AdministersOrganizationAsync(jefe, _organizacion));
    }

    [Fact]
    public async Task ASupervisorOfOneSite_DoesNotReachAnotherSiteOfTheSameChain()
    {
        var supervisor = Guid.NewGuid();
        var suyo = LocalDe(_organizacion);
        var ajeno = LocalDe(_organizacion);
        _sites.FindByIdAsync(suyo.SiteId, Arg.Any<CancellationToken>()).Returns(suyo);
        _sites.FindByIdAsync(ajeno.SiteId, Arg.Any<CancellationToken>()).Returns(ajeno);
        ConVinculos(supervisor,
            Membership.Grant(_organizacion, supervisor, MembershipRole.SUPERVISOR, suyo.SiteId));

        // Esto es el caso que da sentido al puerto: alcanza el local que
        // supervisa aunque los medidores los registrara otra persona, y no
        // alcanza el de al lado aunque sea de la misma cadena.
        Assert.True(await Politica().ReachesSiteAsync(supervisor, suyo.SiteId));
        Assert.False(await Politica().ReachesSiteAsync(supervisor, ajeno.SiteId));
    }

    [Fact]
    public async Task AMembershipInAnotherOrganization_ReachesNothingHere()
    {
        var extrano = Guid.NewGuid();
        var local = LocalDe(_organizacion);
        _sites.FindByIdAsync(local.SiteId, Arg.Any<CancellationToken>()).Returns(local);
        ConVinculos(extrano,
            Membership.Grant(_otraOrganizacion, extrano, MembershipRole.ORG_ADMIN, null));

        Assert.False(await Politica().ReachesSiteAsync(extrano, local.SiteId));
        Assert.False(await Politica().ReachesOrganizationAsync(extrano, _organizacion));
        Assert.False(await Politica().AdministersOrganizationAsync(extrano, _organizacion));
    }

    [Fact]
    public async Task ARevokedMembership_ReachesNothing()
    {
        var antiguo = Guid.NewGuid();
        var local = LocalDe(_organizacion);
        _sites.FindByIdAsync(local.SiteId, Arg.Any<CancellationToken>()).Returns(local);
        var vinculo = Membership.Grant(_organizacion, antiguo, MembershipRole.ORG_ADMIN, null);
        vinculo.Revoke();
        ConVinculos(antiguo, vinculo);

        Assert.False(await Politica().ReachesSiteAsync(antiguo, local.SiteId));
        Assert.False(await Politica().ReachesOrganizationAsync(antiguo, _organizacion));
    }

    [Fact]
    public async Task AnOperator_ReachesTheSiteButDoesNotAdministerTheOrganization()
    {
        var operario = Guid.NewGuid();
        var local = LocalDe(_organizacion);
        _sites.FindByIdAsync(local.SiteId, Arg.Any<CancellationToken>()).Returns(local);
        ConVinculos(operario,
            Membership.Grant(_organizacion, operario, MembershipRole.OPERATOR, local.SiteId));

        Assert.True(await Politica().ReachesSiteAsync(operario, local.SiteId));
        Assert.False(await Politica().AdministersOrganizationAsync(operario, _organizacion));
    }

    [Fact]
    public async Task AZone_InheritsTheReachOfItsSite()
    {
        var jefe = Guid.NewGuid();
        var local = LocalDe(_organizacion);
        var zona = Zone.Register(local.SiteId, "Camara de frio", ZoneType.COLD_STORAGE, false);
        _sites.FindByIdAsync(local.SiteId, Arg.Any<CancellationToken>()).Returns(local);
        _zones.FindByIdAsync(zona.ZoneId, Arg.Any<CancellationToken>()).Returns(zona);
        ConVinculos(jefe, Membership.Grant(_organizacion, jefe, MembershipRole.ORG_ADMIN, null));

        Assert.True(await Politica().ReachesZoneAsync(jefe, zona.ZoneId));
        Assert.False(await Politica().ReachesZoneAsync(Guid.NewGuid(), zona.ZoneId));
    }

    [Fact]
    public async Task AnUnknownSiteOrZone_ReachesNothingInsteadOfFailing()
    {
        var alguien = Guid.NewGuid();
        ConVinculos(alguien, Membership.Grant(_organizacion, alguien, MembershipRole.ORG_ADMIN, null));
        _sites.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Site?)null);
        _zones.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Zone?)null);

        // Un identificador inventado no puede abrir la puerta por el hecho de
        // no encontrarse.
        Assert.False(await Politica().ReachesSiteAsync(alguien, Guid.NewGuid()));
        Assert.False(await Politica().ReachesZoneAsync(alguien, Guid.NewGuid()));
        Assert.False(await Politica().ReachesSiteAsync(alguien, Guid.Empty));
    }

    [Fact]
    public async Task TheOwnerGuard_LetsTheOwnerThroughWithoutAskingTheMemberships()
    {
        var dueno = Guid.NewGuid();
        var politica = Politica();

        await politica.MustOwnOrReachSiteAsync(dueno, dueno, Guid.NewGuid());

        // Ni una consulta: el dueño se resuelve comparando, que es lo que hace
        // que el caso corriente no cueste una lectura de la base.
        await _memberships.DidNotReceive()
            .FindByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheOwnerGuard_RefusesWhenTheCallerIsNeitherOwnerNorMember()
    {
        var ajeno = Guid.NewGuid();
        var local = LocalDe(_organizacion);
        _sites.FindByIdAsync(local.SiteId, Arg.Any<CancellationToken>()).Returns(local);
        ConVinculos(ajeno);

        var error = await Assert.ThrowsAsync<AppException>(() =>
            Politica().MustOwnOrReachSiteAsync(ajeno, Guid.NewGuid(), local.SiteId));
        Assert.Equal(ErrorCode.FORBIDDEN, error.Code);
    }
}
