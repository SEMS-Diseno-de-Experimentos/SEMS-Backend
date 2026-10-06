using Sems.Api.Modules.Alerts.Domain.Model;
using Sems.Api.Shared.Errors;
using Xunit;

namespace Sems.Api.Tests;

/// <summary>
/// Vigilancia de la demanda contra la potencia contratada.
///
/// <para>El valor de esta regla esta en avisar <b>antes</b> de superar lo
/// contratado. Si el umbral se calcula mal, el aviso llega tarde y ya no sirve
/// para nada: el recargo del mes esta hecho.</para>
/// </summary>
public class DemandRuleTests
{
    private static DemandRule Regla(double contratada = 120d, double? aviso = null) =>
        DemandRule.Create(Guid.NewGuid(), Guid.NewGuid(), "T-001", contratada, aviso, true);

    [Fact]
    public void El_umbral_de_aviso_es_el_porcentaje_de_lo_contratado()
    {
        Assert.Equal(102d, Regla(120d, 85d).UmbralDeAvisoKw);
        Assert.Equal(90d, Regla(120d, 75d).UmbralDeAvisoKw);
    }

    [Fact]
    public void Por_defecto_se_avisa_al_ochenta_y_cinco_por_ciento()
    {
        // Deja margen para reaccionar sin disparar avisos en un local que
        // normalmente trabaja al 70-80%.
        Assert.Equal(85d, Regla(120d, null).WarningPercent);
    }

    [Fact]
    public void Por_debajo_del_umbral_no_hay_aviso()
    {
        Assert.Equal(DemandLevel.OK, Regla().Evaluar(90d));
        Assert.Equal(DemandLevel.OK, Regla().Evaluar(101.9d));
    }

    [Fact]
    public void Entre_el_umbral_y_lo_contratado_se_avisa_con_margen()
    {
        var regla = Regla();

        Assert.Equal(DemandLevel.WARNING, regla.Evaluar(102d));
        Assert.Equal(DemandLevel.WARNING, regla.Evaluar(119d));
        // Justo en lo contratado todavia es aviso, no exceso: no se ha pasado.
        Assert.Equal(DemandLevel.WARNING, regla.Evaluar(120d));
    }

    [Fact]
    public void Por_encima_de_lo_contratado_es_exceso()
    {
        Assert.Equal(DemandLevel.EXCEEDED, Regla().Evaluar(120.1d));
        Assert.Equal(DemandLevel.EXCEEDED, Regla().Evaluar(150d));
    }

    [Fact]
    public void El_margen_indica_cuanto_queda_y_cuanto_se_paso()
    {
        var regla = Regla();

        Assert.Equal(15d, regla.MargenKw(105d));
        Assert.Equal(-30d, regla.MargenKw(150d));
    }

    [Fact]
    public void Una_regla_desactivada_no_avisa_de_nada()
    {
        var regla = Regla();
        regla.Deactivate();

        Assert.Equal(DemandLevel.OK, regla.Evaluar(500d));
    }

    [Fact]
    public void Se_rechaza_una_potencia_contratada_no_positiva()
    {
        Assert.Throws<AppException>(() =>
            DemandRule.Create(Guid.NewGuid(), Guid.NewGuid(), null, 0d, null, true));
    }

    [Fact]
    public void Se_rechaza_un_porcentaje_de_aviso_fuera_de_rango()
    {
        Assert.Throws<AppException>(() =>
            DemandRule.Create(Guid.NewGuid(), Guid.NewGuid(), null, 120d, 0d, true));
        Assert.Throws<AppException>(() =>
            DemandRule.Create(Guid.NewGuid(), Guid.NewGuid(), null, 120d, 120d, true));
    }
}
