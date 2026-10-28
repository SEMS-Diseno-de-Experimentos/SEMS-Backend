using System.Net;
using Sems.Api.TestSupport;

namespace Sems.Api.IntegrationTests.Analytics;

/// <summary>
/// Produccion de hallazgos a partir del consumo medido (US35, US37 y US38).
/// </summary>
/// <remarks>
/// Antes, anomalias, recomendaciones y proyecciones se podian dar de alta a
/// mano y consultar, pero nadie las generaba: las tres pantallas estaban
/// condenadas a salir vacias por mucho que el cliente midiera. Lo que se
/// comprueba aqui es que medir basta.
/// </remarks>
public class InsightEndpointsTests : IClassFixture<SemsApiFactory>
{
    private readonly SemsApiFactory _factory;

    public InsightEndpointsTests(SemsApiFactory factory) => _factory = factory;

    private sealed record Layout(TestUser User, Guid SiteId, Guid ZoneId, string MeterId);

    /// <summary>Un local de 120 kW en MT2 con una zona y su medidor.</summary>
    private async Task<Layout> LayoutAsync()
    {
        var user = await _factory.CreateUserAsync();
        var client = user.Client;
        var organization = await client.CreateOrganizationAsync(user.UserId);
        var site = await client.CreateSiteAsync(organization.Id("organization_id"), "T-001", 120m,
            "MT2");
        var siteId = site.Id("site_id");
        var zone = await client.CreateZoneAsync(siteId, "Camaras de frio", "COLD_STORAGE");
        var zoneId = zone.Id("zone_id");

        var meter = await (await client.PostMeterAsync(user.UserId, $"EOS-{Guid.NewGuid():N}",
            siteId, zoneId)).ExpectAsync(HttpStatusCode.Created);

        return new Layout(user, siteId, zoneId, meter.Text("id"));
    }

    /// <summary>
    /// Un dia de consumo, cerrado. El dia en curso no cuenta: esta a medias.
    /// </summary>
    private static async Task DayAsync(Layout layout, int daysAgo, double kwh, double powerWatts,
        double peakKwh = 0)
    {
        var dia = DateTime.UtcNow.Date.AddDays(-daysAgo);

        // Las 10:00 de Peru son las 15:00 UTC: fuera de punta.
        await (await layout.User.Client.PostReadingAsync(layout.User.UserId, layout.MeterId, null,
                powerWatts, energyKwh: kwh - peakKwh, timestamp: dia.AddHours(15)))
            .ExpectAsync(HttpStatusCode.Created);

        if (peakKwh > 0)
        {
            // Las 19:00 de Peru son las 00:00 UTC del dia siguiente: punta.
            await (await layout.User.Client.PostReadingAsync(layout.User.UserId, layout.MeterId,
                    null, powerWatts, energyKwh: peakKwh, timestamp: dia.AddDays(1)))
                .ExpectAsync(HttpStatusCode.Created);
        }
    }

    private static Task<HttpResponseMessage> RecomputeAsync(Layout layout) =>
        layout.User.Client.PostJsonAsync("/api/v1/analytics/insights/recompute",
            new { user_id = layout.User.UserId.ToString() });

    [Fact]
    public async Task Recompute_WithoutMeasuredConsumption_Returns200WithNothingFound()
    {
        var layout = await LayoutAsync();

        var insights = await (await RecomputeAsync(layout)).ExpectAsync(HttpStatusCode.OK);

        Assert.Empty(insights.GetProperty("anomalies").EnumerateArray());
        Assert.Empty(insights.GetProperty("recommendations").EnumerateArray());
        Assert.Empty(insights.GetProperty("bill_predictions").EnumerateArray());
    }

    [Fact]
    public async Task Recompute_ConsumptionConcentratedInOneZoneAtPeak_ProducesSavingAdvice()
    {
        var layout = await LayoutAsync();
        // Diez dias con un tercio del consumo en la franja cara y el pico de
        // potencia rozando lo contratado.
        for (var day = 10; day >= 1; day--)
        {
            await DayAsync(layout, day, 90, 110_000, peakKwh: 30);
        }

        var insights = await (await RecomputeAsync(layout)).ExpectAsync(HttpStatusCode.OK);

        var tipos = insights.GetProperty("recommendations").EnumerateArray()
            .Select(r => r.Text("recommendation_type")).OrderBy(t => t).ToArray();
        Assert.Equal(new[] { "demand_peak", "peak_shift", "zone_concentration" }, tipos);

        var punta = insights.GetProperty("recommendations").EnumerateArray()
            .Single(r => r.Text("recommendation_type") == "peak_shift");
        Assert.Contains("18:00", punta.Text("description"));
        Assert.Equal("PEN", punta.Text("currency"));
        Assert.True(punta.GetProperty("estimated_saving_amount").GetDouble() > 0);
    }

    [Fact]
    public async Task Recompute_MeasuredConsumption_ProjectsTheSiteBill()
    {
        var layout = await LayoutAsync();
        for (var day = 10; day >= 1; day--)
        {
            await DayAsync(layout, day, 100, 60_000, peakKwh: 20);
        }

        var insights = await (await RecomputeAsync(layout)).ExpectAsync(HttpStatusCode.OK);

        var proyeccion = Assert.Single(insights.GetProperty("bill_predictions").EnumerateArray());
        Assert.Equal(layout.SiteId.ToString(), proyeccion.Text("site_id"));
        Assert.True(proyeccion.GetProperty("estimated_amount").GetDouble() > 0);
        // El cargo por potencia se cobra sobre el pico medido, no extrapolado.
        Assert.Equal(60d, proyeccion.GetProperty("estimated_max_demand_kw").GetDouble());
        // Con diez dias medidos el margen declarado es del 10 %.
        Assert.Equal(10d, proyeccion.GetProperty("error_margin_percentage").GetDouble());

        // Y queda guardada: es lo que lee la pantalla de analitica.
        var guardadas = await (await layout.User.Client.GetAsync(
                $"/api/v1/analytics/bill-predictions/user/{layout.User.UserId}"))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.Single(guardadas.EnumerateArray());
    }

    [Fact]
    public async Task Recompute_Twice_DoesNotDuplicateTheFindingsOfTheDay()
    {
        var layout = await LayoutAsync();
        for (var day = 10; day >= 1; day--)
        {
            await DayAsync(layout, day, 90, 110_000, peakKwh: 30);
        }

        await (await RecomputeAsync(layout)).ExpectAsync(HttpStatusCode.OK);
        var segunda = await (await RecomputeAsync(layout)).ExpectAsync(HttpStatusCode.OK);

        // La segunda pasada no encuentra nada nuevo, y lo guardado no crece: con
        // una lectura cada pocos minutos la lista seria inservible.
        Assert.Empty(segunda.GetProperty("recommendations").EnumerateArray());
        Assert.Empty(segunda.GetProperty("bill_predictions").EnumerateArray());

        var guardadas = await (await layout.User.Client.GetAsync(
                $"/api/v1/analytics/recommendations/user/{layout.User.UserId}"))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.Equal(3, guardadas.GetArrayLength());
    }

    [Fact]
    public async Task Recompute_SpikeOnTheLastClosedDay_RaisesAnAnomaly()
    {
        var layout = await LayoutAsync();
        for (var day = 9; day >= 2; day--)
        {
            await DayAsync(layout, day, 100, 50_000);
        }
        // El ultimo dia cerrado dobla la media de los ocho anteriores.
        await DayAsync(layout, 1, 200, 50_000);

        var insights = await (await RecomputeAsync(layout)).ExpectAsync(HttpStatusCode.OK);

        var anomalia = Assert.Single(insights.GetProperty("anomalies").EnumerateArray());
        Assert.Equal("consumption_spike", anomalia.Text("anomaly_type"));
        Assert.Equal("high", anomalia.Text("severity"));
        Assert.Equal(200d, anomalia.GetProperty("actual_kwh").GetDouble());
        Assert.Equal(100d, anomalia.GetProperty("expected_kwh").GetDouble());
    }

    [Fact]
    public async Task Recompute_WithoutAToken_Returns401()
    {
        var response = await _factory.CreateClient().PostJsonAsync(
            "/api/v1/analytics/insights/recompute", new { user_id = Guid.NewGuid().ToString() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
