using System.Net;
using System.Text.Json;
using Sems.Api.TestSupport;
using TechTalk.SpecFlow;

namespace Sems.Api.Specs.Steps;

/// <summary>Factura estimada de un local comercial (EP07).</summary>
[Binding]
public sealed class BillingProjectionSteps
{
    private readonly ApiWorld _world;
    private readonly List<JsonElement> _estimates = new();
    private (string Tariff, decimal Contracted, decimal Peak, decimal OffPeak)? _lastInputs;

    public BillingProjectionSteps(ApiWorld world) => _world = world;

    private JsonElement LastEstimate
    {
        get
        {
            Assert.Equal(HttpStatusCode.OK, _world.Response.StatusCode);
            return _estimates[^1];
        }
    }

    private async Task EstimateAsync(string tariff, decimal contracted, decimal peak, decimal offPeak,
        decimal demand)
    {
        _lastInputs = (tariff, contracted, peak, offPeak);
        var response = await _world.RecordAsync(_world.SignedInUser.Client.PostBillEstimateAsync(
            tariff, contracted, peak, offPeak, demand));
        if (response.StatusCode == HttpStatusCode.OK)
        {
            _estimates.Add(_world.LastBody);
        }
    }

    [When(@"the user estimates the ""(.*)"" bill of a site with ([\d.]+) kW contracted, ([\d.]+) kWh at peak, ([\d.]+) kWh off peak and a maximum demand of ([\d.]+) kW")]
    public Task WhenTheUserEstimatesTheBill(string tariff, decimal contracted, decimal peak,
        decimal offPeak, decimal demand) => EstimateAsync(tariff, contracted, peak, offPeak, demand);

    [When(@"the user estimates the same bill with a maximum demand of ([\d.]+) kW")]
    public Task WhenTheUserEstimatesTheSameBillWithAMaximumDemandOf(decimal demand)
    {
        var inputs = _lastInputs ?? throw new InvalidOperationException("No hay una estimacion previa");
        return EstimateAsync(inputs.Tariff, inputs.Contracted, inputs.Peak, inputs.OffPeak, demand);
    }

    [Then(@"the (energy cost|power cost|subtotal|IGV|total) is ([\d.]+)")]
    public void ThenTheConceptIs(string concept, decimal expected)
    {
        var field = concept switch
        {
            "energy cost" => "energy_cost",
            "power cost" => "power_cost",
            "subtotal" => "subtotal",
            "IGV" => "igv",
            _ => "total"
        };

        Assert.Equal(expected, LastEstimate.Number(field));
    }

    [Then(@"the bill shows a power excess: (yes|no)")]
    public void ThenTheBillShowsAPowerExcess(string answer) =>
        Assert.Equal(answer == "yes", LastEstimate.GetProperty("has_power_excess").GetBoolean());

    [Then(@"the excess power is ([\d.]+) kW")]
    public void ThenTheExcessPowerIs(decimal excessKw) =>
        Assert.Equal(excessKw, LastEstimate.Number("excess_power_kw"));

    [Then(@"both estimates have the same energy cost")]
    public void ThenBothEstimatesHaveTheSameEnergyCost()
    {
        Assert.Equal(2, _estimates.Count);
        Assert.Equal(_estimates[0].Number("energy_cost"), _estimates[1].Number("energy_cost"));
    }

    [Then(@"the second estimate costs more than ([\d.]+) soles extra")]
    public void ThenTheSecondEstimateCostsMoreThanSolesExtra(decimal extra)
    {
        var difference = _estimates[1].Number("total") - _estimates[0].Number("total");
        Assert.True(difference > extra, $"la diferencia fue de S/ {difference}");
    }

    [Then(@"the power share of the subtotal is ([\d.]+) percent")]
    public void ThenThePowerShareOfTheSubtotalIs(decimal percent) =>
        Assert.Equal(percent, LastEstimate.Number("power_share_pct"));
}
