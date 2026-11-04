using System.Net;
using System.Text.Json;
using Sems.Api.TestSupport;

namespace Sems.Api.IntegrationTests.Energy;

/// <summary>
/// Reparto del consumo por zona, entre locales y en el instante (US24, US26 y
/// US39), por HTTP y en snake_case.
/// </summary>
/// <remarks>
/// Las rutas cuelgan de <c>/sites</c> y <c>/organizations</c> aunque el calculo
/// viva en energia: quien pregunta piensa en el local, no en el modulo que
/// guarda las lecturas.
/// </remarks>
public class ConsumptionBreakdownEndpointsTests : IClassFixture<SemsApiFactory>
{
    private readonly SemsApiFactory _factory;

    public ConsumptionBreakdownEndpointsTests(SemsApiFactory factory) => _factory = factory;

    private sealed record Layout(TestUser User, Guid OrganizationId, Guid SiteId, Guid ColdRoomsId,
        Guid SalesFloorId, string ColdMeterId, string SalesMeterId, string UnassignedMeterId);

    /// <summary>
    /// Un local de 120 kW con dos zonas, un medidor en cada una y uno sin zona.
    /// </summary>
    private async Task<Layout> LayoutAsync()
    {
        var user = await _factory.CreateUserAsync();
        var client = user.Client;
        var organization = await client.CreateOrganizationAsync(user.UserId);
        var organizationId = organization.Id("organization_id");
        var site = await client.CreateSiteAsync(organizationId, "T-001", 120m, "MT2");
        var siteId = site.Id("site_id");

        var cold = await client.CreateZoneAsync(siteId, "Camaras de frio", "COLD_STORAGE");
        var sales = await client.CreateZoneAsync(siteId, "Sala de ventas", "SALES_FLOOR");
        var coldId = cold.Id("zone_id");
        var salesId = sales.Id("zone_id");

        return new Layout(user, organizationId, siteId, coldId, salesId,
            await MeterAsync(user, siteId, coldId),
            await MeterAsync(user, siteId, salesId),
            await MeterAsync(user, siteId, null));
    }

    private static async Task<string> MeterAsync(TestUser user, Guid siteId, Guid? zoneId) =>
        (await (await user.Client.PostMeterAsync(user.UserId, $"EOS-{Guid.NewGuid():N}", siteId,
            zoneId)).ExpectAsync(HttpStatusCode.Created)).Text("id");

    /// <summary>Lectura fuera de punta: las 10:00 de Peru son las 15:00 UTC.</summary>
    private static Task ReadAsync(TestUser user, string meterId, double kwh, double powerWatts = 0,
        int utcHour = 15) =>
        user.Client.PostReadingAsync(user.UserId, meterId, null, powerWatts, energyKwh: kwh,
                timestamp: DateTime.UtcNow.Date.AddDays(-2).AddHours(utcHour))
            .ContinueWith(t => t.Result.ExpectAsync(HttpStatusCode.Created)).Unwrap();

    /// <summary>
    /// Si el campo llega vacio. La API omite los nulos, asi que "sin valor" es
    /// tanto la ausencia del campo como un null explicito.
    /// </summary>
    private static bool IsEmpty(JsonElement element, string property) =>
        !element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null;

    private static JsonElement ZoneNamed(JsonElement breakdown, string name) =>
        breakdown.GetProperty("zones").EnumerateArray()
            .Single(z => z.Text("zone_name") == name);

    // ------------------------------------------------------- reparto por zona

    [Fact]
    public async Task ZoneConsumption_SiteWithTwoZones_Returns200WithTheSharesInSnakeCase()
    {
        var layout = await LayoutAsync();
        await ReadAsync(layout.User, layout.ColdMeterId, 300);
        await ReadAsync(layout.User, layout.SalesMeterId, 100);

        var breakdown = await (await layout.User.Client.GetAsync(
                $"/api/v1/sites/{layout.SiteId}/zone-consumption")).ExpectAsync(HttpStatusCode.OK);

        Assert.Equal(layout.SiteId.ToString(), breakdown.Text("site_id"));
        Assert.Equal("Local T-001", breakdown.Text("site_name"));
        Assert.Equal(400d, breakdown.GetProperty("total_kwh").GetDouble());
        Assert.Equal("PEN", breakdown.Text("currency"));
        Assert.Equal(30, breakdown.GetProperty("days").GetInt32());

        var frio = ZoneNamed(breakdown, "Camaras de frio");
        Assert.Equal("COLD_STORAGE", frio.Text("zone_type"));
        Assert.Equal(75d, frio.GetProperty("share_percent").GetDouble());
        Assert.Equal(1, frio.GetProperty("meter_count").GetInt32());
        Assert.Equal(300 * 0.2395m, frio.Number("estimated_energy_cost"));
    }

    [Fact]
    public async Task ZoneConsumption_MeterWithoutAZone_ItsConsumptionIsReportedApart()
    {
        var layout = await LayoutAsync();
        await ReadAsync(layout.User, layout.ColdMeterId, 150);
        await ReadAsync(layout.User, layout.UnassignedMeterId, 50);

        var breakdown = await (await layout.User.Client.GetAsync(
                $"/api/v1/sites/{layout.SiteId}/zone-consumption")).ExpectAsync(HttpStatusCode.OK);

        var unassigned = breakdown.GetProperty("unassigned");
        Assert.Equal(50d, unassigned.GetProperty("kwh").GetDouble());
        Assert.Equal(25d, unassigned.GetProperty("share_percent").GetDouble());
        Assert.True(IsEmpty(unassigned, "zone_id"));
    }

    [Fact]
    public async Task ZoneConsumption_UnknownSite_Returns404()
    {
        var layout = await LayoutAsync();

        var response = await layout.User.Client.GetAsync(
            $"/api/v1/sites/{Guid.NewGuid()}/zone-consumption");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ZoneConsumption_WithoutAToken_Returns401()
    {
        var layout = await LayoutAsync();

        var response = await _factory.CreateClient().GetAsync(
            $"/api/v1/sites/{layout.SiteId}/zone-consumption");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -------------------------------------------------- comparacion de locales

    [Fact]
    public async Task SiteConsumption_OrganizationWithTwoSites_ComparesThemPerContractedKw()
    {
        var layout = await LayoutAsync();
        var client = layout.User.Client;
        await client.UseUnlimitedSitesPlanAsync(layout.User.UserId);
        var second = await client.CreateSiteAsync(layout.OrganizationId, "T-002", 60m, "BT3");
        var secondMeter = await MeterAsync(layout.User, second.Id("site_id"), null);

        await ReadAsync(layout.User, layout.ColdMeterId, 1200, 90_000);
        await ReadAsync(layout.User, secondMeter, 600, 40_000);

        var comparison = await (await client.GetAsync(
                $"/api/v1/organizations/{layout.OrganizationId}/site-consumption"))
            .ExpectAsync(HttpStatusCode.OK);

        Assert.Equal(1800d, comparison.GetProperty("total_kwh").GetDouble());
        var sites = comparison.GetProperty("sites").EnumerateArray().ToList();
        Assert.Equal(new[] { "T-001", "T-002" }, sites.Select(s => s.Text("site_code")));

        // Los dos rinden igual por kW contratado aunque uno consuma el doble.
        Assert.Equal(10d, sites[0].GetProperty("kwh_per_contracted_kw").GetDouble());
        Assert.Equal(10d, sites[1].GetProperty("kwh_per_contracted_kw").GetDouble());
        Assert.Equal(90d, sites[0].GetProperty("peak_demand_kw").GetDouble());
        Assert.Equal(120m, sites[0].Number("contracted_power_kw"));
    }

    [Fact]
    public async Task SiteConsumption_OrganizationWithoutReadings_ReturnsEverySiteAtZero()
    {
        var layout = await LayoutAsync();

        var comparison = await (await layout.User.Client.GetAsync(
                $"/api/v1/organizations/{layout.OrganizationId}/site-consumption"))
            .ExpectAsync(HttpStatusCode.OK);

        var site = Assert.Single(comparison.GetProperty("sites").EnumerateArray());
        Assert.Equal(0d, site.GetProperty("kwh").GetDouble());
        Assert.Equal(3, site.GetProperty("meter_count").GetInt32());
    }

    // --------------------------------------------------------- consumo actual

    [Fact]
    public async Task CurrentConsumption_SiteWithReadings_AddsTheLatestPowerOfEachMeter()
    {
        var layout = await LayoutAsync();
        await ReadAsync(layout.User, layout.ColdMeterId, 10, 80_000);
        await ReadAsync(layout.User, layout.SalesMeterId, 5, 22_000);

        var current = await (await layout.User.Client.GetAsync(
                $"/api/v1/sites/{layout.SiteId}/consumption/current"))
            .ExpectAsync(HttpStatusCode.OK);

        Assert.Equal(102d, current.GetProperty("power_kw").GetDouble());
        Assert.Equal(120m, current.Number("contracted_power_kw"));
        // 102 de 120 kW: el 85 %, justo el umbral con el que avisa la regla.
        Assert.Equal(85d, current.GetProperty("occupancy_percent").GetDouble());
        Assert.Equal(3, current.GetProperty("meter_count").GetInt32());
        Assert.False(IsEmpty(current, "measured_at"));
    }

    [Fact]
    public async Task CurrentConsumption_SiteThatNeverReported_ReturnsZeroWithoutFailing()
    {
        var layout = await LayoutAsync();

        var current = await (await layout.User.Client.GetAsync(
                $"/api/v1/sites/{layout.SiteId}/consumption/current"))
            .ExpectAsync(HttpStatusCode.OK);

        Assert.Equal(0d, current.GetProperty("power_kw").GetDouble());
        Assert.True(IsEmpty(current, "measured_at"));
    }
}
