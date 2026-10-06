using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Sems.Api.TestSupport;

/// <summary>
/// Peticiones de uso comun contra la API, respetando el contrato JSON de cada
/// modulo: camelCase en IAM y Device Management; snake_case en Energy,
/// Analytics, Alerts, Organizations y Payments; snake_case en la peticion y
/// PascalCase en la respuesta de Subscriptions.
/// </summary>
public static class ApiRequests
{
    /// <summary>Sin politica de nombres: cada campo sale tal como se escribe en la prueba.</summary>
    private static readonly JsonSerializerOptions AsWritten = new();

    private static readonly Random Random = new();

    // ------------------------------------------------------------------- HTTP

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url,
        object body) => client.PostAsJsonAsync(url, body, AsWritten);

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string url,
        object body) => client.PutAsJsonAsync(url, body, AsWritten);

    public static Task<HttpResponseMessage> PatchJsonAsync(this HttpClient client, string url,
        object body) => client.PatchAsJsonAsync(url, body, AsWritten);

    /// <summary>Cuerpo de la respuesta como JSON (un objeto vacio si no hay cuerpo).</summary>
    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Comprueba el codigo de estado y devuelve el cuerpo. Si no coincide, el
    /// error lleva el cuerpo de la respuesta para que el fallo se entienda.
    /// </summary>
    public static async Task<JsonElement> ExpectAsync(this HttpResponseMessage response,
        HttpStatusCode expected)
    {
        if (response.StatusCode != expected)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} " +
                $"devolvio {(int)response.StatusCode} {response.StatusCode} y se esperaba " +
                $"{(int)expected} {expected}. Cuerpo: {body}");
        }
        return await response.ReadJsonAsync();
    }

    // ------------------------------------------------------------ utilidades

    /// <summary>RUC valido y distinto en cada llamada: 11 digitos que empiezan por 20.</summary>
    public static string NewTaxId()
    {
        lock (Random)
        {
            return "20" + Random.Next(100_000_000, 999_999_999).ToString(CultureInfo.InvariantCulture);
        }
    }

    public static string Text(this JsonElement element, string property) =>
        element.GetProperty(property).GetString()!;

    public static Guid Id(this JsonElement element, string property) =>
        Guid.Parse(element.GetProperty(property).GetString()!);

    public static decimal Number(this JsonElement element, string property) =>
        element.GetProperty(property).GetDecimal();

    // --------------------------------------------------------- organizaciones

    public static Task<HttpResponseMessage> PostOrganizationAsync(this HttpClient client,
        Guid ownerUserId, string taxId, string legalName = "Minimarket Los Andes S.A.C.") =>
        client.PostJsonAsync("/api/v1/organizations", new
        {
            legal_name = legalName,
            trade_name = (string?)null,
            tax_id = taxId,
            business_type = "SUPERMARKET",
            owner_user_id = ownerUserId.ToString()
        });

    public static async Task<JsonElement> CreateOrganizationAsync(this HttpClient client,
        Guid ownerUserId, string? taxId = null, string legalName = "Minimarket Los Andes S.A.C.") =>
        await (await client.PostOrganizationAsync(ownerUserId, taxId ?? NewTaxId(), legalName))
            .ExpectAsync(HttpStatusCode.Created);

    public static Task<HttpResponseMessage> PostSiteAsync(this HttpClient client,
        Guid organizationId, string siteCode, decimal contractedPowerKw = 120m,
        string tariffCategory = "MT2") =>
        client.PostJsonAsync($"/api/v1/organizations/{organizationId}/sites", new
        {
            site_code = siteCode,
            name = $"Local {siteCode}",
            address = "Av. Larco 123",
            district = "Miraflores",
            floor_area_m2 = 450m,
            contracted_power_kw = contractedPowerKw,
            tariff_category = tariffCategory
        });

    public static async Task<JsonElement> CreateSiteAsync(this HttpClient client,
        Guid organizationId, string siteCode, decimal contractedPowerKw = 120m,
        string tariffCategory = "MT2") =>
        await (await client.PostSiteAsync(organizationId, siteCode, contractedPowerKw, tariffCategory))
            .ExpectAsync(HttpStatusCode.Created);

    public static Task<HttpResponseMessage> PostZoneAsync(this HttpClient client, Guid siteId,
        string name, string zoneType) =>
        client.PostJsonAsync($"/api/v1/sites/{siteId}/zones", new
        {
            name,
            zone_type = zoneType
        });

    public static async Task<JsonElement> CreateZoneAsync(this HttpClient client, Guid siteId,
        string name = "Cold rooms", string zoneType = "COLD_STORAGE") =>
        await (await client.PostZoneAsync(siteId, name, zoneType)).ExpectAsync(HttpStatusCode.Created);

    // ------------------------------------------------------------ dispositivos

    public static Task<HttpResponseMessage> PostDeviceAsync(this HttpClient client, Guid userId,
        string siteId, string? zoneId, string externalCode, string name = "Compressor submeter") =>
        client.PostJsonAsync("/api/v1/device-management/devices", new
        {
            externalDeviceCode = externalCode,
            userId = userId.ToString(),
            siteId,
            zoneId,
            deviceName = name,
            deviceType = "REFRIGERATION",
            brand = "Schneider",
            model = "PM5110",
            connectionProtocol = "WIFI"
        });

    public static async Task<JsonElement> CreateDeviceAsync(this HttpClient client, Guid userId,
        Guid siteId, Guid? zoneId, string? externalCode = null) =>
        await (await client.PostDeviceAsync(userId, siteId.ToString(), zoneId?.ToString(),
            externalCode ?? $"SM-{Guid.NewGuid():N}")).ExpectAsync(HttpStatusCode.Created);

    // ------------------------------------------------------------------ energia

    public static Task<HttpResponseMessage> PostMeterAsync(this HttpClient client, Guid userId,
        string meterSerial) =>
        client.PostJsonAsync("/api/v1/energy-meters", new
        {
            user_id = userId.ToString(),
            meter_serial = meterSerial,
            model = "EOS-3F",
            brand = "EOS",
            location = "Main board",
            firmware_version = "1.0.0",
            max_power_watts = 250_000.0
        });

    public static Task<HttpResponseMessage> PostReadingAsync(this HttpClient client, Guid userId,
        string meterId, string? deviceId, double powerWatts, double frequency = 60.0,
        double energyKwh = 1.0) =>
        client.PostJsonAsync("/api/v1/energy-readings", new
        {
            user_id = userId.ToString(),
            meter_id = meterId,
            device_id = deviceId,
            power_watts = powerWatts,
            voltage = 380.0,
            current = 150.0,
            frequency,
            energy_kwh = energyKwh,
            reading_type = "real_time",
            phase = "three"
        });

    public static Task<HttpResponseMessage> PostBillEstimateAsync(this HttpClient client,
        string tariffCategory, decimal contractedPowerKw, decimal kwhPeak, decimal kwhOffPeak,
        decimal maxDemandKw) =>
        client.PostJsonAsync("/api/v1/energy/bill-estimate", new
        {
            tariff_category = tariffCategory,
            contracted_power_kw = contractedPowerKw,
            kwh_peak = kwhPeak,
            kwh_off_peak = kwhOffPeak,
            max_demand_kw = maxDemandKw
        });

    // ------------------------------------------------------------------ alertas

    public static Task<HttpResponseMessage> PostDemandRuleAsync(this HttpClient client,
        Guid siteId, Guid userId, double contractedPowerKw, double? warningPercent) =>
        client.PostJsonAsync("/api/v1/demand-rules", new
        {
            site_id = siteId.ToString(),
            user_id = userId.ToString(),
            rule_name = "Contracted power watch",
            contracted_power_kw = contractedPowerKw,
            warning_percent = warningPercent
        });

    public static Task<HttpResponseMessage> PostDemandEvaluationAsync(this HttpClient client,
        Guid siteId, double demandKw) =>
        client.PostJsonAsync($"/api/v1/sites/{siteId}/demand-evaluations", new
        {
            demand_kw = demandKw
        });
}
