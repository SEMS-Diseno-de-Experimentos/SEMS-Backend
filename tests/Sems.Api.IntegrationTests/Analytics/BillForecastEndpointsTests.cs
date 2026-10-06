using System.Net;
using Sems.Api.TestSupport;

namespace Sems.Api.IntegrationTests.Analytics;

/// <summary>
/// Proyeccion de la factura de un local. Analytics no calcula por su cuenta:
/// pide el importe a Energy a traves del puerto <c>IBillCalculator</c>.
/// </summary>
public class BillForecastEndpointsTests : IClassFixture<SemsApiFactory>
{
    private readonly SemsApiFactory _factory;

    public BillForecastEndpointsTests(SemsApiFactory factory) => _factory = factory;

    private static object ForecastBody(Guid userId, Guid siteId, string tariffCategory) => new
    {
        user_id = userId.ToString(),
        site_id = siteId.ToString(),
        tariff_category = tariffCategory,
        contracted_power_kw = 250.0,
        prediction_year = 2026,
        prediction_month = 10,
        period_start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        period_end = new DateTime(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc),
        kwh_peak = 12000.0,
        kwh_off_peak = 48000.0,
        max_demand_kw = 280.0,
        error_margin_percentage = 5.0
    };

    [Fact]
    public async Task ForecastSiteBill_SameInputsAsBillEstimate_Returns201WithTheSameTotal()
    {
        var user = await _factory.CreateUserAsync();
        var estimate = await (await user.Client.PostBillEstimateAsync("MT2", 250m, 12000m, 48000m, 280m))
            .ExpectAsync(HttpStatusCode.OK);

        var response = await user.Client.PostJsonAsync("/api/v1/analytics/bill-predictions/forecast",
            ForecastBody(user.UserId, Guid.NewGuid(), "MT2"));

        var forecast = await response.ExpectAsync(HttpStatusCode.Created);
        Assert.Equal(estimate.Number("total"), forecast.Number("estimated_amount"));
        Assert.Equal(estimate.Number("energy_cost"), forecast.Number("energy_cost"));
        Assert.Equal(estimate.Number("power_cost"), forecast.Number("power_cost"));
        Assert.Equal(60000m, forecast.Number("estimated_kwh"));
    }

    [Fact]
    public async Task ForecastSiteBill_CreatedForecast_IsListedForTheUser()
    {
        var user = await _factory.CreateUserAsync();
        var siteId = Guid.NewGuid();
        await (await user.Client.PostJsonAsync("/api/v1/analytics/bill-predictions/forecast",
            ForecastBody(user.UserId, siteId, "MT2"))).ExpectAsync(HttpStatusCode.Created);

        var listing = await (await user.Client.GetAsync($"/api/v1/analytics/bill-predictions/user/{user.UserId}"))
            .ExpectAsync(HttpStatusCode.OK);

        var forecast = Assert.Single(listing.EnumerateArray());
        Assert.Equal(siteId.ToString(), forecast.Text("site_id"));
    }

    [Fact]
    public async Task ForecastSiteBill_UnknownTariffCategory_Returns400()
    {
        var user = await _factory.CreateUserAsync();

        var response = await user.Client.PostJsonAsync("/api/v1/analytics/bill-predictions/forecast",
            ForecastBody(user.UserId, Guid.NewGuid(), "XYZ"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
