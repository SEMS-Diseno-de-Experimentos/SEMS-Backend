using Sems.Api.Modules.Energy.Domain.Model;
using Sems.Api.Modules.Energy.Infrastructure;
using Sems.Api.Shared.Errors;
using Xunit;

namespace Sems.Api.Tests.Energy;

/// <summary>
/// <see cref="PowerReading"/>, <see cref="TariffCategories"/> y el adaptador
/// simulado del proveedor de tarifas.
/// </summary>
public class EnergyValueObjectTests
{
    private readonly MockPlusEnergiaAdapter _adapter = new();

    // ------------------------------------------------------------ PowerReading

    [Theory]
    [InlineData(-1, 220, 5)]
    [InlineData(1000, -1, 5)]
    [InlineData(1000, 220, -1)]
    public void PowerReading_NegativePowerVoltageOrCurrent_ThrowsValidationError(double power,
        double voltage, double current)
    {
        var error = Assert.Throws<AppException>(() =>
            new PowerReading(power, voltage, current, 60, 1));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
    }

    [Theory]
    [InlineData(44.9)]
    [InlineData(65.1)]
    [InlineData(70)]
    public void PowerReading_FrequencyOutsideTheGridRange_IsRejected(double frequency)
    {
        var error = Assert.Throws<AppException>(() =>
            new PowerReading(1000, 220, 5, frequency, 1));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
    }

    [Theory]
    [InlineData(45)]
    [InlineData(60)]
    [InlineData(65)]
    public void PowerReading_FrequencyAtTheEdgesOfTheRange_IsAccepted(double frequency)
    {
        Assert.Equal(frequency, new PowerReading(1000, 220, 5, frequency, 1).Frequency);
    }

    [Fact]
    public void PowerFactor_PowerAboveTheApparentPower_IsCappedAtOne()
    {
        Assert.Equal(1.0, new PowerReading(2000, 220, 5, 60, 1).PowerFactor());
    }

    // ------------------------------------------------------- TariffCategories

    [Theory]
    [InlineData("bt5b", "BT5B")]
    [InlineData(" bt3 ", "BT3")]
    [InlineData("BT4", "BT4")]
    [InlineData("mt2", "MT2")]
    [InlineData("MT3", "MT3")]
    public void Normalize_KnownCategory_ReturnsItInUppercase(string raw, string expected)
    {
        Assert.Equal(expected, TariffCategories.Normalize(raw));
        Assert.True(TariffCategories.IsKnown(raw));
    }

    [Theory]
    [InlineData("XYZ")]
    [InlineData("BT6")]
    [InlineData("MT1")]
    [InlineData("")]
    [InlineData(null)]
    public void Normalize_UnknownOrEmptyCategory_ThrowsValidationError(string? raw)
    {
        var error = Assert.Throws<AppException>(() => TariffCategories.Normalize(raw));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
        Assert.False(TariffCategories.IsKnown(raw));
    }

    // ------------------------------------------------- adaptador de tarifas

    [Fact]
    public void CurrentTariff_Bt5b_HasNoPowerCharge()
    {
        var tariff = _adapter.CurrentTariff("BT5B");

        Assert.Equal(0m, tariff.PotenciaPorKwMes);
        Assert.Equal(0m, tariff.ExcesoDePotenciaPorKwMes);
        Assert.Equal(tariff.EnergiaPuntaPorKwh, tariff.EnergiaFueraDePuntaPorKwh);
    }

    [Theory]
    [InlineData("MT2", "BT3")]
    [InlineData("MT3", "BT4")]
    public void CurrentTariff_MediumVoltage_HasCheaperEnergyAndDearerPowerThanLowVoltage(
        string medium, string low)
    {
        var mt = _adapter.CurrentTariff(medium);
        var bt = _adapter.CurrentTariff(low);

        Assert.True(mt.EnergiaPuntaPorKwh < bt.EnergiaPuntaPorKwh);
        Assert.True(mt.EnergiaFueraDePuntaPorKwh < bt.EnergiaFueraDePuntaPorKwh);
        Assert.True(mt.PotenciaPorKwMes > bt.PotenciaPorKwMes);
    }

    [Theory]
    [InlineData("BT3")]
    [InlineData("BT4")]
    [InlineData("MT2")]
    [InlineData("MT3")]
    public void CurrentTariff_CategoryWithPowerCharge_PricesTheExcessAboveNormalPower(string category)
    {
        var tariff = _adapter.CurrentTariff(category);

        Assert.True(tariff.PotenciaPorKwMes > 0m);
        Assert.True(tariff.ExcesoDePotenciaPorKwMes > tariff.PotenciaPorKwMes);
        Assert.Equal(category, tariff.TariffCategory);
        Assert.Equal(0.18m, tariff.Igv);
    }

    [Fact]
    public void CurrentPrice_SameDay_IsStable()
    {
        Assert.Equal(_adapter.CurrentPrice().PricePerKwh, _adapter.CurrentPrice().PricePerKwh);
    }
}
