using NSubstitute;
using Sems.Api.Modules.Analytics.Application;
using Sems.Api.Modules.Analytics.Domain.Model;
using Sems.Api.Modules.Analytics.Domain.Repositories;
using Sems.Api.Modules.Analytics.Domain.Services;
using Sems.Api.Modules.Analytics.Infrastructure;
using Sems.Api.Modules.Energy.Domain.Services;
using Sems.Api.Modules.Energy.Infrastructure;
using Sems.Api.Shared.Errors;
using Xunit;

namespace Sems.Api.Tests.Analytics;

/// <summary>
/// Casos de uso de analitica con el puerto <see cref="IBillCalculator"/>
/// simulado, y su implementacion <see cref="EnergyBillCalculator"/>.
/// </summary>
public class AnalyticsServiceTests
{
    private readonly IBillPredictionRepository _predictions = Substitute.For<IBillPredictionRepository>();
    private readonly IRecommendationRepository _recommendations = Substitute.For<IRecommendationRepository>();
    private readonly IAnomalyRepository _anomalies = Substitute.For<IAnomalyRepository>();
    private readonly IConsumptionRankingRepository _rankings = Substitute.For<IConsumptionRankingRepository>();
    private readonly IBillCalculator _calculator = Substitute.For<IBillCalculator>();
    private readonly AnalyticsService _service;

    public AnalyticsServiceTests()
    {
        _predictions.SaveAsync(Arg.Any<BillPrediction>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<BillPrediction>());
        _rankings.SaveAsync(Arg.Any<ConsumptionRanking>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<ConsumptionRanking>());

        _service = new AnalyticsService(_predictions, _recommendations, _anomalies,
            Substitute.For<IDeviceIdentificationRepository>(), _rankings, _calculator);
    }

    [Fact]
    public async Task ForecastSiteBillAsync_SiteForecast_StoresTheCalculatorAmountAndTheTotalConsumption()
    {
        _calculator.Estimate("MT2", 12000, 48000, 280, 250).Returns(
            new EstimatedBill(14868.00, 17228.00, 12.80, 37888.38, "PEN", 0.2395));
        var siteId = Guid.NewGuid().ToString();

        var prediction = await _service.ForecastSiteBillAsync("user-1", siteId, "MT2", 250, 2026, 10,
            new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), 12000, 48000, 280, 5);

        Assert.Equal(37888.38, prediction.EstimatedAmount);
        Assert.Equal(60000, prediction.EstimatedKwh);
        Assert.Equal(17228.00, prediction.PowerCost);
        Assert.Equal(siteId, prediction.SiteId);
        await _predictions.Received(1).SaveAsync(prediction, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void EnergyBillCalculator_Estimate_DelegatesToTheEnergyTariffAndReturnsTheSameTotal()
    {
        var pricing = new MockPlusEnergiaAdapter();
        var expected = pricing.CurrentTariff("MT2").Calcular(12000m, 48000m, 280m, 250m);

        var estimate = new EnergyBillCalculator(pricing).Estimate("MT2", 12000, 48000, 280, 250);

        Assert.Equal((double)expected.Total, estimate.Total);
        Assert.Equal(37888.38, estimate.Total);
        Assert.Equal((double)expected.CostoPotencia, estimate.PowerCost);
        Assert.Equal("PEN", estimate.Currency);
    }

    [Theory]
    [InlineData("XYZ")]
    [InlineData("BT6")]
    public void EnergyBillCalculator_UnknownCategory_ThrowsValidationErrorWithoutCallingTheProvider(
        string category)
    {
        var pricing = Substitute.For<IEnergyPricingProvider>();

        var error = Assert.Throws<AppException>(() =>
            new EnergyBillCalculator(pricing).Estimate(category, 12000, 48000, 280, 250));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
        pricing.DidNotReceiveWithAnyArgs().CurrentTariff(default);
    }

    [Fact]
    public async Task ApplyRecommendationAsync_UnknownRecommendation_ThrowsNotFound()
    {
        var error = await Assert.ThrowsAsync<AppException>(
            () => _service.ApplyRecommendationAsync(Guid.NewGuid()));

        Assert.Equal(ErrorCode.NOT_FOUND, error.Code);
    }

    [Fact]
    public async Task ResolveAnomalyAsync_UnknownAnomaly_ThrowsNotFound()
    {
        var error = await Assert.ThrowsAsync<AppException>(
            () => _service.ResolveAnomalyAsync(Guid.NewGuid()));

        Assert.Equal(ErrorCode.NOT_FOUND, error.Code);
    }

    [Fact]
    public async Task CreateRankingAsync_RankedDevices_KeepsTheirOrder()
    {
        var items = new[]
        {
            new RankingItem(1, "dev-1", "Cold room compressor", 820.5, 557.94, 61.2, "PEN"),
            new RankingItem(2, "dev-2", "Lighting", 310.0, 210.80, 23.1, "PEN")
        };

        var ranking = await _service.CreateRankingAsync("user-1", "monthly", DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow, items);

        Assert.Equal(new[] { "dev-1", "dev-2" }, ranking.Rankings().Select(i => i.DeviceId));
    }
}
