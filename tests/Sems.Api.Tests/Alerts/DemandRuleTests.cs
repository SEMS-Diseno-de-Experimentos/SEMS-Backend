using Sems.Api.Modules.Alerts.Domain.Model;
using Sems.Api.Shared.Errors;
using Xunit;

namespace Sems.Api.Tests.Alerts;

/// <summary>
/// Vigilancia de la demanda contra la potencia contratada: 120 kW contratados y
/// aviso al 85 % (umbral de 102 kW).
///
/// <para>El valor de esta regla esta en avisar <b>antes</b> de superar lo
/// contratado. Si el umbral se calcula mal, el aviso llega tarde y ya no sirve
/// para nada: el recargo del mes esta hecho.</para>
/// </summary>
public class DemandRuleTests
{
    private static DemandRule Regla(double contratada = 120d, double? aviso = 85d) =>
        DemandRule.Create(Guid.NewGuid(), Guid.NewGuid(), "T-001", contratada, aviso, true);

    [Theory]
    [InlineData(101.99, DemandLevel.OK)]
    [InlineData(102, DemandLevel.WARNING)]
    [InlineData(119.99, DemandLevel.WARNING)]
    [InlineData(120, DemandLevel.WARNING)]
    [InlineData(120.01, DemandLevel.EXCEEDED)]
    public void Evaluar_DemandAroundTheThresholdAndTheContractedPower_ReturnsTheExpectedLevel(
        double demand, DemandLevel expected)
    {
        Assert.Equal(expected, Regla().Evaluar(demand));
    }

    [Theory]
    [InlineData(120, 85, 102)]
    [InlineData(120, 75, 90)]
    [InlineData(250, 90, 225)]
    public void UmbralDeAvisoKw_AnyRule_IsThePercentageOfTheContractedPower(double contracted,
        double percent, double threshold)
    {
        Assert.Equal(threshold, Regla(contracted, percent).UmbralDeAvisoKw, 6);
    }

    [Fact]
    public void Create_WithoutPercentage_WarnsAtEightyFivePercent()
    {
        // Deja margen para reaccionar sin disparar avisos en un local que
        // normalmente trabaja al 70-80%.
        var regla = Regla(120d, null);

        Assert.Equal(85d, regla.WarningPercent);
        Assert.Equal(102d, regla.UmbralDeAvisoKw, 6);
    }

    [Theory]
    [InlineData(119.99, DemandLevel.OK)]
    [InlineData(120, DemandLevel.WARNING)]
    public void Evaluar_WarningAtOneHundredPercent_OnlyWarnsAtTheContractedPower(double demand,
        DemandLevel expected)
    {
        Assert.Equal(expected, Regla(120d, 100d).Evaluar(demand));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(100.5d)]
    [InlineData(120d)]
    public void Create_WarningPercentOutOfRange_ThrowsValidationError(double percent)
    {
        var error = Assert.Throws<AppException>(() =>
            DemandRule.Create(Guid.NewGuid(), Guid.NewGuid(), null, 120d, percent, true));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-10d)]
    public void Create_NonPositiveContractedPower_ThrowsValidationError(double contracted)
    {
        var error = Assert.Throws<AppException>(() =>
            DemandRule.Create(Guid.NewGuid(), Guid.NewGuid(), null, contracted, null, true));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
    }

    [Fact]
    public void Create_EmptySite_ThrowsValidationError()
    {
        var error = Assert.Throws<AppException>(() =>
            DemandRule.Create(Guid.Empty, Guid.NewGuid(), null, 120d, null, true));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
    }

    [Theory]
    [InlineData(105, 15)]
    [InlineData(150, -30)]
    [InlineData(120, 0)]
    public void MargenKw_MeasuredDemand_IsWhatIsLeftOrWhatWasExceeded(double demand, double margin)
    {
        Assert.Equal(margin, Regla().MargenKw(demand), 6);
    }

    [Fact]
    public void Evaluar_DeactivatedRule_NeverWarns()
    {
        var regla = Regla();
        regla.Deactivate();

        Assert.Equal(DemandLevel.OK, regla.Evaluar(500d));
        Assert.False(regla.Active);
    }

    // ------------------------------------------------- silencio entre avisos
    //
    // La evaluacion ahora la dispara cada lectura. Un medidor que reporta cada
    // minuto evaluaria la regla sesenta veces por hora, y sin freno el cliente
    // recibiria sesenta alertas mientras el local trabaja al 90 % de lo
    // contratado. El freno vive en la regla y no en quien la llama, para que
    // valga igual para la evaluacion automatica y para la manual.

    [Fact]
    public void MereceAviso_FirstTimeAtAnyLevelAboveOk_SaysYes()
    {
        var regla = Regla();

        Assert.True(regla.MereceAviso(DemandLevel.WARNING, DateTime.UtcNow));
        Assert.True(regla.MereceAviso(DemandLevel.EXCEEDED, DateTime.UtcNow));
    }

    [Fact]
    public void MereceAviso_LevelOk_SaysNo()
    {
        Assert.False(Regla().MereceAviso(DemandLevel.OK, DateTime.UtcNow));
    }

    [Fact]
    public void MereceAviso_SameLevelWithinTheSilence_SaysNo()
    {
        var regla = Regla();
        var ahora = DateTime.UtcNow;
        regla.AnotarAviso(DemandLevel.WARNING, ahora);

        Assert.False(regla.MereceAviso(DemandLevel.WARNING,
            ahora + DemandRule.Silencio - TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void MereceAviso_SameLevelOnceTheSilenceIsOver_SaysYes()
    {
        var regla = Regla();
        var ahora = DateTime.UtcNow;
        regla.AnotarAviso(DemandLevel.WARNING, ahora);

        Assert.True(regla.MereceAviso(DemandLevel.WARNING, ahora + DemandRule.Silencio));
    }

    [Fact]
    public void MereceAviso_LevelGetsWorseWithinTheSilence_SaysYes()
    {
        // Pasar de acercarse a superar lo contratado es noticia nueva: el cargo
        // del mes acaba de quedar fijado y callarlo media hora no tiene sentido.
        var regla = Regla();
        var ahora = DateTime.UtcNow;
        regla.AnotarAviso(DemandLevel.WARNING, ahora);

        Assert.True(regla.MereceAviso(DemandLevel.EXCEEDED, ahora + TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void MereceAviso_LevelImprovesWithinTheSilence_SaysNo()
    {
        var regla = Regla();
        var ahora = DateTime.UtcNow;
        regla.AnotarAviso(DemandLevel.EXCEEDED, ahora);

        Assert.False(regla.MereceAviso(DemandLevel.WARNING, ahora + TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void AnotarNormalidad_AfterAnAlert_LetsTheNextEpisodeWarnAtOnce()
    {
        // El episodio termino. Si no se olvidara el ultimo aviso, un pico nuevo
        // diez minutos despues se quedaria sin avisar.
        var regla = Regla();
        var ahora = DateTime.UtcNow;
        regla.AnotarAviso(DemandLevel.WARNING, ahora);

        regla.AnotarNormalidad();

        Assert.Null(regla.LastAlertLevel);
        Assert.Null(regla.LastAlertAt);
        Assert.True(regla.MereceAviso(DemandLevel.WARNING, ahora + TimeSpan.FromMinutes(1)));
    }
}

