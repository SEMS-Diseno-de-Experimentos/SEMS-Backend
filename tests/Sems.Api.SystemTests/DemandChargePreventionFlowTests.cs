using System.Net;
using System.Text.Json;
using Sems.Api.TestSupport;

namespace Sems.Api.SystemTests;

/// <summary>
/// Flujo 2: prevencion del cargo por potencia (US24, US28, US29, US30, US31,
/// US35, US36, TS05).
/// </summary>
/// <remarks>
/// Un local de 120 kW en MT2 con aviso al 85 % (102 kW). Encadena
/// Organizations, Device Management, Energy, Alerts, Analytics y el envio de
/// correo que dispara <c>AlertTriggered</c>.
/// </remarks>
public class DemandChargePreventionFlowTests : IClassFixture<SemsApiFactory>
{
    private const string AlertSubject = "Consumption alert at your site";
    private static readonly DateTime FirstReading = new(2026, 10, 7, 23, 0, 0, DateTimeKind.Utc);

    private readonly SemsApiFactory _factory;

    public DemandChargePreventionFlowTests(SemsApiFactory factory) => _factory = factory;

    private sealed record MeteredSite(TestUser Manager, Guid SiteId, string MeterId, string DeviceId);

    /// <summary>Local de 120 kW en MT2, regla al 85 %, medidor y submedidor.</summary>
    private async Task<MeteredSite> MeteredSiteAsync()
    {
        var manager = await _factory.CreateUserAsync();
        var client = manager.Client;
        var organization = await client.CreateOrganizationAsync(manager.UserId);
        var site = await client.CreateSiteAsync(organization.Id("organization_id"), "T-001", 120m, "MT2");
        var siteId = site.Id("site_id");

        // US28: la regla avisa al 85 % de lo contratado.
        var rule = await (await client.PostDemandRuleAsync(siteId, manager.UserId, 120, 85))
            .ExpectAsync(HttpStatusCode.Created);
        Assert.Equal(102m, rule.Number("warning_threshold_kw"));

        var meter = await (await client.PostMeterAsync(manager.UserId, $"EOS-{Guid.NewGuid():N}",
            siteId)).ExpectAsync(HttpStatusCode.Created);
        var device = await client.CreateDeviceAsync(manager.UserId, siteId, null,
            $"SM-MAIN-{Guid.NewGuid():N}");

        return new MeteredSite(manager, siteId, meter.Text("id"), device.Text("deviceId"));
    }

    /// <summary>
    /// Registra la lectura. La demanda la evalua el propio sistema.
    /// </summary>
    /// <remarks>
    /// Ya no hace falta llamar a la evaluacion: la lectura la dispara. Es lo que
    /// pide US29, porque el pico que fija el cargo del mes dura minutos y nadie
    /// esta pulsando un boton cuando pasa.
    /// </remarks>
    private static async Task ReadAsync(MeteredSite metered, double demandKw, int minute) =>
        await (await metered.Manager.Client.PostReadingAsync(metered.Manager.UserId,
                metered.MeterId, metered.DeviceId, demandKw * 1000,
                timestamp: FirstReading.AddMinutes(minute)))
            .ExpectAsync(HttpStatusCode.Created);

    /// <summary>Evalua la demanda a mano, como hace la pantalla del local.</summary>
    private static async Task<JsonElement> EvaluateAsync(MeteredSite metered, double demandKw) =>
        await (await metered.Manager.Client.PostDemandEvaluationAsync(metered.SiteId, demandKw))
            .ExpectAsync(HttpStatusCode.OK);

    private static async Task<JsonElement> ReadAndEvaluateAsync(MeteredSite metered, double demandKw,
        int minute)
    {
        await ReadAsync(metered, demandKw, minute);
        return await EvaluateAsync(metered, demandKw);
    }

    private static string ResultOf(JsonElement alerts) =>
        alerts.GetArrayLength() == 0 ? "none" : Assert.Single(alerts.EnumerateArray()).Text("severity");

    private static object ForecastBody(MeteredSite metered, double maxDemandKw) => new
    {
        user_id = metered.Manager.UserId.ToString(),
        site_id = metered.SiteId.ToString(),
        tariff_category = "MT2",
        contracted_power_kw = 120.0,
        prediction_year = 2026,
        prediction_month = 10,
        period_start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        period_end = new DateTime(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc),
        kwh_peak = 6000.0,
        kwh_off_peak = 24000.0,
        max_demand_kw = maxDemandKw,
        error_margin_percentage = 5.0
    };

    [Fact]
    public async Task DemandChargePrevention_DemandClimbsPastTheContractedPower_WarnsThenRaisesCriticalAndBillsTheExcess()
    {
        var metered = await MeteredSiteAsync();
        var client = metered.Manager.Client;

        // US29, US30, US31 y TS05: basta registrar las lecturas; el sistema
        // evalua la demanda de cada una contra la regla del local.
        var demands = new[] { 90d, 104d, 118d, 132d };
        for (var i = 0; i < demands.Length; i++)
        {
            await ReadAsync(metered, demands[i], i);
        }

        // Evaluar a mano justo despues no duplica el aviso: la regla se calla
        // media hora tras avisar del mismo nivel. Sin ese freno, un medidor que
        // reporta cada minuto generaria una alerta por lectura.
        Assert.Equal("none", ResultOf(await EvaluateAsync(metered, 132d)));

        // US24: la ultima lectura es el consumo actual.
        var current = await (await client.GetAsync(
                $"/api/v1/energy/devices/{metered.DeviceId}/consumption/current"))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.Equal(132_000d, current.GetProperty("power_watts").GetDouble());

        // Dos alertas, no cuatro: la de 104 kW avisa, la de 118 kW se calla por
        // ser el mismo nivel, y la de 132 kW vuelve a avisar porque empeorar
        // siempre es noticia nueva.
        var alerts = await (await client.GetAsync($"/api/v1/users/{metered.Manager.UserId}/alerts"))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.Equal(new[] { "CRITICAL", "WARNING" },
            alerts.EnumerateArray().Select(a => a.Text("severity")).OrderBy(s => s));
        var emails = _factory.Emails.SentTo(metered.Manager.Email, AlertSubject);
        Assert.Equal(2, emails.Count);
        Assert.Contains(emails, e => e.Body.Contains("12 kW above the 120 kW contracted"));

        // US35 y US36: la factura con demanda maxima de 132 kW cobra 12 kW de exceso.
        var bill = await (await client.PostBillEstimateAsync("MT2", 120m, 6000m, 24000m, 132m))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.True(bill.GetProperty("has_power_excess").GetBoolean());
        Assert.Equal(12m, bill.Number("excess_power_kw"));
        Assert.Equal(120m * 58.40m + 12m * 87.60m, bill.Number("power_cost"));
        Assert.Equal(bill.Number("subtotal") + bill.Number("igv"), bill.Number("total"));

        // La proyeccion de Analytics da el mismo total.
        var forecast = await (await client.PostJsonAsync("/api/v1/analytics/bill-predictions/forecast",
            ForecastBody(metered, 132))).ExpectAsync(HttpStatusCode.Created);
        Assert.Equal(bill.Number("total"), forecast.Number("estimated_amount"));
    }

    [Fact]
    public async Task DemandChargePrevention_DemandNeverAboveTheWarning_RaisesNoAlertsEmailsOrExcess()
    {
        var metered = await MeteredSiteAsync();
        var client = metered.Manager.Client;

        var demands = new[] { 85d, 95d, 101.9d };
        for (var i = 0; i < demands.Length; i++)
        {
            Assert.Equal("none", ResultOf(await ReadAndEvaluateAsync(metered, demands[i], i)));
        }

        var alerts = await (await client.GetAsync($"/api/v1/users/{metered.Manager.UserId}/alerts"))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.Empty(alerts.EnumerateArray());
        Assert.Empty(_factory.Emails.SentTo(metered.Manager.Email, AlertSubject));

        var bill = await (await client.PostBillEstimateAsync("MT2", 120m, 6000m, 24000m, 101.9m))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.False(bill.GetProperty("has_power_excess").GetBoolean());
        Assert.Equal(0m, bill.Number("excess_power_kw"));
        Assert.Equal(101.9m * 58.40m, bill.Number("power_cost"));
    }
}
