using Sems.Api.Modules.Organizations.Domain.Model;
using Sems.Api.Shared.Errors;
using Xunit;

namespace Sems.Api.Tests;

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

    // ------------------------------------------------------------ organizacion

    [Fact]
    public void El_ruc_debe_tener_once_digitos()
    {
        Assert.Throws<AppException>(() =>
            Organization.Register("X", null, "20512345", BusinessType.SUPERMARKET));
        Assert.Throws<AppException>(() =>
            Organization.Register("X", null, "2051234567A", BusinessType.SUPERMARKET));
    }

    [Fact]
    public void Una_organizacion_archivada_no_se_reactiva()
    {
        var cadena = UnaCadena();
        cadena.Archive();

        Assert.Throws<AppException>(() => cadena.Reactivate());
    }

    // ------------------------------------------------------------------ local

    [Fact]
    public void Un_local_necesita_potencia_contratada_mayor_que_cero()
    {
        Assert.Throws<AppException>(() => Site.Register(Guid.NewGuid(), "T-001", "Miraflores",
            null, null, null, contractedPowerKw: 0m, TariffCategory.MT2));
    }

    [Fact]
    public void El_exceso_de_potencia_se_calcula_sobre_lo_contratado()
    {
        var local = Site.Register(Guid.NewGuid(), "T-001", "Miraflores", null, null, null,
            contractedPowerKw: 120m, TariffCategory.MT2);

        Assert.Equal(30m, local.ExcesoDePotencia(150m));
        Assert.Equal(0m, local.ExcesoDePotencia(120m));
        Assert.Equal(0m, local.ExcesoDePotencia(90m));
    }

    [Fact]
    public void Solo_la_categoria_BT5B_no_cobra_potencia()
    {
        Assert.False(TariffCategory.BT5B.CobraPorPotencia());
        Assert.True(TariffCategory.BT3.CobraPorPotencia());
        Assert.True(TariffCategory.MT2.CobraPorPotencia());
    }

    // ------------------------------------------------------------------- zona

    [Fact]
    public void Una_camara_frigorifica_funciona_fuera_de_horario_por_defecto()
    {
        // Lo usa el modulo de alertas: consumo de madrugada en una camara es
        // normal y en la sala de ventas es un equipo olvidado encendido.
        var camara = Zone.Register(Guid.NewGuid(), "Camaras", ZoneType.COLD_STORAGE, null);
        var sala = Zone.Register(Guid.NewGuid(), "Sala de ventas", ZoneType.SALES_FLOOR, null);

        Assert.True(camara.OperatesOffHours);
        Assert.False(sala.OperatesOffHours);
    }

    [Fact]
    public void Lo_que_diga_el_usuario_manda_sobre_el_valor_deducido()
    {
        var sala = Zone.Register(Guid.NewGuid(), "Sala 24h", ZoneType.SALES_FLOOR,
            operatesOffHours: true);

        Assert.True(sala.OperatesOffHours);
    }

    // -------------------------------------------------------------- permisos

    [Fact]
    public void Un_administrador_no_puede_quedar_atado_a_un_solo_local()
    {
        // Si se permitiera, la cadena se quedaria sin nadie capaz de dar de alta
        // el siguiente local.
        Assert.Throws<AppException>(() => Membership.Grant(Guid.NewGuid(), Guid.NewGuid(),
            MembershipRole.ORG_ADMIN, siteId: Guid.NewGuid()));
    }

    [Fact]
    public void Un_supervisor_sin_local_asignado_se_rechaza()
    {
        // Dejarlo pasar le daria acceso a toda la cadena por descuido, que es lo
        // contrario de lo que se pretende con ese papel.
        Assert.Throws<AppException>(() => Membership.Grant(Guid.NewGuid(), Guid.NewGuid(),
            MembershipRole.SUPERVISOR, siteId: null));
    }

    [Fact]
    public void El_supervisor_solo_alcanza_a_su_local()
    {
        var suLocal = Guid.NewGuid();
        var otroLocal = Guid.NewGuid();
        var vinculo = Membership.Grant(Guid.NewGuid(), Guid.NewGuid(),
            MembershipRole.SUPERVISOR, suLocal);

        Assert.True(vinculo.AlcanzaAlLocal(suLocal));
        Assert.False(vinculo.AlcanzaAlLocal(otroLocal));
    }

    [Fact]
    public void El_administrador_alcanza_a_cualquier_local_de_su_cadena()
    {
        var vinculo = Membership.Grant(Guid.NewGuid(), Guid.NewGuid(),
            MembershipRole.ORG_ADMIN, null);

        Assert.True(vinculo.AlcanzaAlLocal(Guid.NewGuid()));
        Assert.True(vinculo.AlcanzaAlLocal(Guid.NewGuid()));
    }

    [Fact]
    public void El_operario_consulta_pero_no_modifica()
    {
        var operario = Membership.Grant(Guid.NewGuid(), Guid.NewGuid(),
            MembershipRole.OPERATOR, null);

        Assert.False(operario.PuedeModificar);
        Assert.False(operario.PuedeAdministrarLaOrganizacion);
    }

    [Fact]
    public void El_supervisor_modifica_pero_no_administra_la_cadena()
    {
        var supervisor = Membership.Grant(Guid.NewGuid(), Guid.NewGuid(),
            MembershipRole.SUPERVISOR, Guid.NewGuid());

        Assert.True(supervisor.PuedeModificar);
        Assert.False(supervisor.PuedeAdministrarLaOrganizacion);
    }

    [Fact]
    public void Un_vinculo_revocado_deja_de_dar_acceso()
    {
        var local = Guid.NewGuid();
        var vinculo = Membership.Grant(Guid.NewGuid(), Guid.NewGuid(),
            MembershipRole.SUPERVISOR, local);
        vinculo.Revoke();

        Assert.False(vinculo.AlcanzaAlLocal(local));
        Assert.False(vinculo.PuedeModificar);
    }
}
