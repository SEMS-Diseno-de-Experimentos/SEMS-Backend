using Sems.Api.Modules.Organizations.Domain.Model;
using Sems.Api.Shared.Errors;
using Xunit;

namespace Sems.Api.Tests.Organizations;

/// <summary>
/// Reglas de organizaciones, locales, zonas y permisos.
///
/// <para>Los permisos son la parte delicada: una regla mal puesta no falla, deja
/// ver datos a quien no debe. Eso no lo detecta ninguna prueba de humo.</para>
/// </summary>
public class OrganizationDomainTests
{
    private static Organization UnaCadena() =>
        Organization.Register("Supermercados Andinos S.A.C.", "MercaAndes", "20512345678",
            BusinessType.SUPERMARKET);

    private static Site UnLocal(decimal contractedPowerKw = 120m, string code = "T-001") =>
        Site.Register(Guid.NewGuid(), code, "Miraflores", null, null, null, contractedPowerKw,
            TariffCategory.MT2);

    // ------------------------------------------------------------ organizacion

    [Theory]
    [InlineData("2060123456")]
    [InlineData("206012345678")]
    [InlineData("2060123456A")]
    [InlineData("20601-23456")]
    [InlineData("20512345")]
    public void Register_TaxIdThatIsNotElevenDigits_ThrowsValidationError(string taxId)
    {
        var error = Assert.Throws<AppException>(() =>
            Organization.Register("Bodega Central", null, taxId, BusinessType.CONVENIENCE_STORE));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
        Assert.Equal("tax_id must be 11 digits", error.Message);
    }

    [Fact]
    public void Register_ElevenDigitTaxIdWithSpaces_IsAcceptedTrimmed()
    {
        var organization = Organization.Register("Farmacia Vega", null, "  10456789012  ",
            BusinessType.OTHER);

        Assert.Equal("10456789012", organization.TaxId);
        Assert.Equal(OrgStatus.ACTIVE, organization.Status);
    }

    [Fact]
    public void Reactivate_ArchivedOrganization_ThrowsValidationError()
    {
        var cadena = UnaCadena();
        cadena.Archive();

        Assert.Throws<AppException>(() => cadena.Reactivate());
    }

    // ------------------------------------------------------------------ local

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void RegisterSite_NonPositiveContractedPower_ThrowsValidationError(decimal power)
    {
        var error = Assert.Throws<AppException>(() => UnLocal(power));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
    }

    [Theory]
    [InlineData(150, 30)]
    [InlineData(120, 0)]
    [InlineData(90, 0)]
    public void ExcesoDePotencia_SiteOf120Kw_ReturnsTheDemandAboveTheContractedPower(
        decimal demand, decimal expectedExcess)
    {
        Assert.Equal(expectedExcess, UnLocal(120m).ExcesoDePotencia(demand));
    }

    [Theory]
    [InlineData("BT5B", TariffCategory.BT5B)]
    [InlineData("BT3", TariffCategory.BT3)]
    [InlineData("BT4", TariffCategory.BT4)]
    [InlineData("MT2", TariffCategory.MT2)]
    [InlineData("mt3", TariffCategory.MT3)]
    public void ToTariffCategory_CategoryOfTheSchedule_IsAccepted(string raw, TariffCategory expected)
    {
        Assert.Equal(expected, OrganizationEnums.ToTariffCategory(raw));
    }

    [Theory]
    [InlineData("BT6")]
    [InlineData("MT1")]
    [InlineData("")]
    [InlineData(null)]
    public void ToTariffCategory_UnknownOrEmptyCategory_ThrowsValidationError(string? raw)
    {
        var error = Assert.Throws<AppException>(() => OrganizationEnums.ToTariffCategory(raw));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
    }

    [Theory]
    [InlineData(TariffCategory.BT5B, false)]
    [InlineData(TariffCategory.BT3, true)]
    [InlineData(TariffCategory.BT4, true)]
    [InlineData(TariffCategory.MT2, true)]
    [InlineData(TariffCategory.MT3, true)]
    public void CobraPorPotencia_EachCategory_OnlyBt5bDoesNotChargeForDemand(TariffCategory category,
        bool chargesForDemand)
    {
        Assert.Equal(chargesForDemand, category.CobraPorPotencia());
    }

    [Fact]
    public void RegisterSite_LowercaseCode_IsNormalizedToUppercase()
    {
        Assert.Equal("T-001", UnLocal(code: " t-001 ").SiteCode);
    }

    [Fact]
    public void RegisterSite_NewSite_DoesNotExcludeSundaysFromPeakByDefault()
    {
        // El pliego fija la hora punta todos los dias; la exclusion de domingos
        // es solo a solicitud del cliente.
        Assert.False(UnLocal().ExcludesSundaysFromPeak);
    }

    // ------------------------------------------------------------------- zona

    [Fact]
    public void RegisterZone_InASite_BelongsToThatSite()
    {
        var site = UnLocal();

        var zone = Zone.Register(site.SiteId, "Cold rooms", ZoneType.COLD_STORAGE, null);

        Assert.Equal(site.SiteId, zone.SiteId);
        Assert.Equal(OrgStatus.ACTIVE, zone.Status);
    }

    [Fact]
    public void RegisterZone_ColdStorageWithoutSaying_OperatesOffHoursByDefault()
    {
        // Lo usa el modulo de alertas: consumo de madrugada en una camara es
        // normal y en la sala de ventas es un equipo olvidado encendido.
        var camara = Zone.Register(Guid.NewGuid(), "Camaras", ZoneType.COLD_STORAGE, null);
        var sala = Zone.Register(Guid.NewGuid(), "Sala de ventas", ZoneType.SALES_FLOOR, null);

        Assert.True(camara.OperatesOffHours);
        Assert.False(sala.OperatesOffHours);
    }

    [Fact]
    public void RegisterZone_ExplicitValue_PrevailsOverTheDeducedOne()
    {
        var sala = Zone.Register(Guid.NewGuid(), "Sala 24h", ZoneType.SALES_FLOOR,
            operatesOffHours: true);

        Assert.True(sala.OperatesOffHours);
    }

    // -------------------------------------------------------------- permisos

    [Fact]
    public void Grant_AdministratorLimitedToOneSite_ThrowsValidationError()
    {
        // Si se permitiera, la cadena se quedaria sin nadie capaz de dar de alta
        // el siguiente local.
        var error = Assert.Throws<AppException>(() => Membership.Grant(Guid.NewGuid(), Guid.NewGuid(),
            MembershipRole.ORG_ADMIN, siteId: Guid.NewGuid()));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
    }

    [Fact]
    public void Grant_SupervisorWithoutSite_ThrowsValidationError()
    {
        // Dejarlo pasar le daria acceso a toda la cadena por descuido, que es lo
        // contrario de lo que se pretende con ese papel.
        var error = Assert.Throws<AppException>(() => Membership.Grant(Guid.NewGuid(), Guid.NewGuid(),
            MembershipRole.SUPERVISOR, siteId: null));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
    }

    [Fact]
    public void AlcanzaAlLocal_Supervisor_OnlyReachesTheirOwnSite()
    {
        var suLocal = Guid.NewGuid();
        var otroLocal = Guid.NewGuid();
        var vinculo = Membership.Grant(Guid.NewGuid(), Guid.NewGuid(),
            MembershipRole.SUPERVISOR, suLocal);

        Assert.True(vinculo.AlcanzaAlLocal(suLocal));
        Assert.False(vinculo.AlcanzaAlLocal(otroLocal));
    }

    [Fact]
    public void AlcanzaAlLocal_Administrator_ReachesAnySiteOfTheOrganization()
    {
        var vinculo = Membership.Grant(Guid.NewGuid(), Guid.NewGuid(),
            MembershipRole.ORG_ADMIN, null);

        Assert.True(vinculo.AlcanzaAlLocal(Guid.NewGuid()));
        Assert.True(vinculo.AlcanzaAlLocal(Guid.NewGuid()));
    }

    [Fact]
    public void Grant_Operator_CanReadButNotModify()
    {
        var operario = Membership.Grant(Guid.NewGuid(), Guid.NewGuid(),
            MembershipRole.OPERATOR, null);

        Assert.False(operario.PuedeModificar);
        Assert.False(operario.PuedeAdministrarLaOrganizacion);
    }

    [Fact]
    public void Grant_Supervisor_CanModifyButNotManageTheOrganization()
    {
        var supervisor = Membership.Grant(Guid.NewGuid(), Guid.NewGuid(),
            MembershipRole.SUPERVISOR, Guid.NewGuid());

        Assert.True(supervisor.PuedeModificar);
        Assert.False(supervisor.PuedeAdministrarLaOrganizacion);
    }

    [Fact]
    public void Revoke_ActiveMembership_StopsGrantingAccess()
    {
        var local = Guid.NewGuid();
        var vinculo = Membership.Grant(Guid.NewGuid(), Guid.NewGuid(),
            MembershipRole.SUPERVISOR, local);
        vinculo.Revoke();

        Assert.False(vinculo.IsActive);
        Assert.False(vinculo.AlcanzaAlLocal(local));
        Assert.False(vinculo.PuedeModificar);
    }

    [Fact]
    public void Reinstate_RevokedMembership_IsActiveAgainWithTheNewRole()
    {
        var local = Guid.NewGuid();
        var vinculo = Membership.Grant(Guid.NewGuid(), Guid.NewGuid(),
            MembershipRole.OPERATOR, null);
        vinculo.Revoke();

        vinculo.Reinstate(MembershipRole.SUPERVISOR, local);

        Assert.True(vinculo.IsActive);
        Assert.Equal(MembershipRole.SUPERVISOR, vinculo.Role);
        Assert.True(vinculo.AlcanzaAlLocal(local));
        Assert.True(vinculo.PuedeModificar);
    }
}
