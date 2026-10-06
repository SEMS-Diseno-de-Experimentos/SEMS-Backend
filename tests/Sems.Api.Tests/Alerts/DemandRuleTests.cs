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
}
