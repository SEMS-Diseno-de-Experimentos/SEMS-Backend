using System.Net;
using Sems.Api.TestSupport;

namespace Sems.Api.IntegrationTests.Energy;

/// <summary>
/// Factura estimada, tarifa publicada, medidores y lecturas. Contrato en
/// snake_case.
/// </summary>
public class EnergyEndpointsTests : IClassFixture<SemsApiFactory>
{
    private readonly SemsApiFactory _factory;

    public EnergyEndpointsTests(SemsApiFactory factory) => _factory = factory;

    // ------------------------------------------------------- factura estimada

    [Fact]
    public async Task EstimateBill_Section526Example_Returns200WithSubtotalIgvAndTotal()
    {
        var user = await _factory.CreateUserAsync();

        var response = await user.Client.PostBillEstimateAsync("MT2", 250m, 12000m, 48000m, 280m);

        var bill = await response.ExpectAsync(HttpStatusCode.OK);
        Assert.Equal(14868.00m, bill.Number("energy_cost"));
        Assert.Equal(17228.00m, bill.Number("power_cost"));
        Assert.Equal(32108.80m, bill.Number("subtotal"));
        Assert.Equal(5779.58m, bill.Number("igv"));
        Assert.Equal(37888.38m, bill.Number("total"));
        Assert.Equal(53.7m, bill.Number("power_share_pct"));
        Assert.Equal(30m, bill.Number("excess_power_kw"));
    }

    [Fact]
    public async Task EstimateBill_DemandEqualToContracted_ReturnsNoPowerExcess()
    {
        var user = await _factory.CreateUserAsync();

        var bill = await (await user.Client.PostBillEstimateAsync("MT2", 120m, 6000m, 24000m, 120m))
            .ExpectAsync(HttpStatusCode.OK);

        Assert.False(bill.GetProperty("has_power_excess").GetBoolean());
        Assert.Equal(0m, bill.Number("excess_power_kw"));
    }

    [Fact]
    public async Task EstimateBill_SameConsumptionHigherPeak_OnlyPowerCostGrows()
    {
        var user = await _factory.CreateUserAsync();

        var lowPeak = await (await user.Client.PostBillEstimateAsync("MT2", 120m, 6000m, 24000m, 110m))
            .ExpectAsync(HttpStatusCode.OK);
        var highPeak = await (await user.Client.PostBillEstimateAsync("MT2", 120m, 6000m, 24000m, 150m))
            .ExpectAsync(HttpStatusCode.OK);

        Assert.Equal(lowPeak.Number("energy_cost"), highPeak.Number("energy_cost"));
        Assert.Equal(lowPeak.Number("fixed_charge"), highPeak.Number("fixed_charge"));
        Assert.True(highPeak.Number("power_cost") > lowPeak.Number("power_cost"));
    }

    [Fact]
    public async Task EstimateBill_UnknownTariffCategory_Returns400()
    {
        var user = await _factory.CreateUserAsync();

        var response = await user.Client.PostBillEstimateAsync("XYZ", 120m, 6000m, 24000m, 110m);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(-1, 24000, 120)]
    [InlineData(6000, -1, 120)]
    [InlineData(6000, 24000, 0)]
    public async Task EstimateBill_NegativeConsumptionOrZeroContractedPower_Returns400(
        decimal kwhPeak, decimal kwhOffPeak, decimal contractedPowerKw)
    {
        var user = await _factory.CreateUserAsync();

        var response = await user.Client.PostBillEstimateAsync("MT2", contractedPowerKw, kwhPeak,
            kwhOffPeak, 110m);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EstimateBill_WithoutToken_Returns401()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.PostBillEstimateAsync("MT2", 120m, 6000m, 24000m, 110m);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -------------------------------------------------------- tarifa publicada

    [Fact]
    public async Task GetTariff_Mt2_Returns200WithPeakHoursEveryDay()
    {
        var user = await _factory.CreateUserAsync();

        var tariff = await (await user.Client.GetAsync("/api/v1/energy/tariffs/MT2"))
            .ExpectAsync(HttpStatusCode.OK);

        Assert.Equal("MT2", tariff.Text("tariff_category"));
        Assert.Equal("18:00-23:00 every day", tariff.Text("peak_hours"));
        Assert.Equal(58.40m, tariff.Number("power_per_kw_month"));
        Assert.Equal(87.60m, tariff.Number("excess_power_per_kw_month"));
    }

    [Fact]
    public async Task GetTariff_UnknownCategory_Returns400()
    {
        var user = await _factory.CreateUserAsync();

        var response = await user.Client.GetAsync("/api/v1/energy/tariffs/XYZ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --------------------------------------------------- medidores y lecturas

    [Fact]
    public async Task RegisterMeter_RepeatedSerialNumber_Returns409()
    {
        var user = await _factory.CreateUserAsync();
        var serial = $"EOS-{Guid.NewGuid():N}";
        await (await user.Client.PostMeterAsync(user.UserId, serial)).ExpectAsync(HttpStatusCode.Created);

        var response = await user.Client.PostMeterAsync(user.UserId, serial);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RecordReading_ValidReading_Returns201()
    {
        var user = await _factory.CreateUserAsync();
        var meter = await (await user.Client.PostMeterAsync(user.UserId, $"EOS-{Guid.NewGuid():N}"))
            .ExpectAsync(HttpStatusCode.Created);

        var response = await user.Client.PostReadingAsync(user.UserId, meter.Text("id"), null,
            powerWatts: 95_000, frequency: 60);

        var reading = await response.ExpectAsync(HttpStatusCode.Created);
        Assert.Equal(95_000d, reading.GetProperty("power_watts").GetDouble());
        Assert.Equal(meter.Text("id"), reading.Text("meter_id"));
    }

    [Fact]
    public async Task RecordReading_SeventyHertz_Returns400()
    {
        var user = await _factory.CreateUserAsync();

        var response = await user.Client.PostReadingAsync(user.UserId, Guid.NewGuid().ToString(), null,
            powerWatts: 95_000, frequency: 70);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
