using Sems.Api.Modules.Analytics.Domain.Model;
using Xunit;

namespace Sems.Api.Tests.Analytics;

/// <summary>
/// <see cref="Anomaly"/>, <see cref="Recommendation"/> y
/// <see cref="ConsumptionRanking"/>.
/// </summary>
public class AnalyticsDomainTests
{
    [Theory]
    [InlineData(150, 100, 50)]
    [InlineData(50, 100, -50)]
    [InlineData(100, 100, 0)]
    public void Detect_ActualAgainstExpectedConsumption_ComputesTheRelativeDeviation(double actual,
        double expected, double deviation)
    {
        var anomaly = Anomaly.Detect("user-1", "dev-1", "spike", null, "high", actual, expected);

        Assert.Equal(deviation, anomaly.DeviationPercentage, 6);
        Assert.Equal(Anomaly.StatusOpen, anomaly.Status);
    }

    [Fact]
    public void Detect_WithoutExpectedConsumption_ReturnsZeroDeviation()
    {
        // Sin esta guarda, un consumo esperado de cero dividiria por cero.
        var anomaly = Anomaly.Detect("user-1", "dev-1", "spike", null, "high", 40, 0);

        Assert.Equal(0, anomaly.DeviationPercentage);
    }

    [Fact]
    public void Resolve_Twice_KeepsTheFirstResolutionDate()
    {
        var anomaly = Anomaly.Detect("user-1", null, "spike", null, "low", 120, 100);
        anomaly.Resolve();
        var first = anomaly.ResolvedAt;

        anomaly.Resolve();

        Assert.Equal(Anomaly.StatusResolved, anomaly.Status);
        Assert.Equal(first, anomaly.ResolvedAt);
    }

    [Fact]
    public void Apply_Twice_KeepsTheFirstApplicationDate()
    {
        var recommendation = Recommendation.Create("user-1", null, "peak_shift",
            "Move pre-freezing off peak", null, 900, 216, null);
        recommendation.Apply();
        var first = recommendation.AppliedAt;

        recommendation.Apply();

        Assert.Equal(Recommendation.StatusApplied, recommendation.Status);
        Assert.NotNull(first);
        Assert.Equal(first, recommendation.AppliedAt);
    }

    [Fact]
    public void Create_RecommendationWithoutCurrency_DefaultsToSoles()
    {
        var recommendation = Recommendation.Create("user-1", null, "peak_shift", "Title", null, 1, 1,
            currency: null);

        Assert.Equal("PEN", recommendation.Currency);
        Assert.Equal(Recommendation.StatusPending, recommendation.Status);
    }

    [Fact]
    public void Rankings_StoredItems_ComeBackInTheSameOrder()
    {
        var ranking = ConsumptionRanking.Create("user-1", "monthly", DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow, new[]
            {
                new RankingItem(1, "dev-1", "Compressor", 820.5, 557.94, 61.2, "PEN"),
                new RankingItem(2, "dev-2", "Lighting", 310.0, 210.80, 23.1, "PEN")
            });

        var items = ranking.Rankings();

        Assert.Equal(new[] { 1, 2 }, items.Select(i => i.Rank));
        Assert.Equal("Compressor", items[0].DeviceName);
    }

    [Fact]
    public void Rankings_WithoutItems_ReturnsAnEmptyList()
    {
        var ranking = ConsumptionRanking.Create("user-1", "monthly", DateTime.UtcNow, DateTime.UtcNow,
            null);

        Assert.Empty(ranking.Rankings());
    }
}
