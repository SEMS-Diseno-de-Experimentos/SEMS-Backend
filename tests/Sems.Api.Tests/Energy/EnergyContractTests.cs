using System.Text.Json;
using Sems.Api.Modules.Energy.Domain.Model;
using Sems.Api.Modules.Energy.Interfaces;
using Xunit;

namespace Sems.Api.Tests.Energy;

/// <summary>
/// El modulo de energia nacio en FastAPI y serializaba en snake_case. El
/// frontend lee <c>power_watts</c>, <c>energy_kwh</c>, <c>user_id</c>...
///
/// <para>C# serializa en camelCase por defecto, asi que si alguien quita un
/// <c>[JsonPropertyName]</c> el JSON sigue siendo valido y la API sigue
/// respondiendo 200: el fallo solo se nota cuando la pantalla del dashboard sale
/// en blanco. Esta prueba convierte ese fallo silencioso en un fallo de build.</para>
/// </summary>
public class EnergyContractTests
{
    [Fact]
    public void ReadingResponse_Serialized_UsesSnakeCase()
    {
        var response = new EnergyResources.ReadingResponse(
            Id: Guid.NewGuid().ToString(),
            UserId: Guid.NewGuid().ToString(),
            MeterId: Guid.NewGuid().ToString(),
            DeviceId: null,
            PowerWatts: 1200.5,
            Voltage: 220,
            Current: 5.4,
            Frequency: 60,
            EnergyKwh: 3.2,
            Timestamp: DateTime.UtcNow,
            ReadingType: "instant",
            Phase: "L1",
            CreatedAt: DateTime.UtcNow);

        var json = JsonSerializer.Serialize(response);

        Assert.Contains("\"power_watts\"", json);
        Assert.Contains("\"energy_kwh\"", json);
        Assert.Contains("\"reading_type\"", json);
        Assert.Contains("\"created_at\"", json);
        // Si esto aparece, alguien perdio la etiqueta y el frontend deja de leer el campo.
        Assert.DoesNotContain("\"powerWatts\"", json);
        Assert.DoesNotContain("\"energyKwh\"", json);
    }

    [Fact]
    public void CreateReadingRequest_SnakeCaseBody_IsDeserialized()
    {
        const string body = """
            {
              "user_id": "11111111-1111-1111-1111-111111111111",
              "meter_id": "22222222-2222-2222-2222-222222222222",
              "power_watts": 1500.0,
              "voltage": 220.0,
              "current": 6.8,
              "frequency": 60.0,
              "energy_kwh": 4.5,
              "reading_type": "instant",
              "phase": "L1"
            }
            """;

        var request = JsonSerializer.Deserialize<EnergyResources.CreateReadingRequest>(body);

        Assert.NotNull(request);
        Assert.Equal("11111111-1111-1111-1111-111111111111", request!.UserId);
        Assert.Equal(1500.0, request.PowerWatts);
        Assert.Equal(4.5, request.EnergyKwh);
        Assert.Equal("instant", request.ReadingType);
    }

    [Fact]
    public void ConsumptionResponse_Serialized_KeepsTheDashboardFieldNames()
    {
        var response = new EnergyResources.ConsumptionResponse(
            Id: Guid.NewGuid().ToString(),
            UserId: Guid.NewGuid().ToString(),
            DeviceId: Guid.NewGuid().ToString(),
            DeviceName: "Refrigeradora",
            MeterId: null,
            TotalKwh: 12.5,
            CostEstimateSoles: 8.75,
            PeriodStart: DateTime.UtcNow.AddDays(-30),
            PeriodEnd: DateTime.UtcNow,
            PeakPowerWatts: 1800,
            AveragePowerWatts: 600,
            ReadingCount: 240,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow);

        var json = JsonSerializer.Serialize(response);

        Assert.Contains("\"total_kwh\"", json);
        Assert.Contains("\"cost_estimate_soles\"", json);
        Assert.Contains("\"peak_power_watts\"", json);
        Assert.Contains("\"reading_count\"", json);
    }

    [Fact]
    public void EstimateBillRequest_SnakeCaseBody_IsDeserialized()
    {
        const string body = """
            {
              "tariff_category": "MT2",
              "contracted_power_kw": 250,
              "kwh_peak": 12000,
              "kwh_off_peak": 48000,
              "max_demand_kw": 280
            }
            """;

        var request = JsonSerializer.Deserialize<EnergyResources.EstimateBillRequest>(body);

        Assert.NotNull(request);
        Assert.Equal("MT2", request!.TariffCategory);
        Assert.Equal(250m, request.ContractedPowerKw);
        Assert.Equal(48000m, request.KwhOffPeak);
        Assert.Equal(280m, request.MaxDemandKw);
    }

    [Fact]
    public void BillEstimateResponse_Serialized_UsesSnakeCase()
    {
        var breakdown = new BillBreakdown(12000m, 48000m, 280m, 250m, 30m, 14868m, 17228m, 12.80m,
            32108.80m, 5779.58m, 37888.38m, "PEN");

        var json = JsonSerializer.Serialize(EnergyResources.BillEstimateResponse.From(breakdown));

        Assert.Contains("\"energy_cost\"", json);
        Assert.Contains("\"power_cost\"", json);
        Assert.Contains("\"has_power_excess\"", json);
        Assert.Contains("\"power_share_pct\"", json);
        Assert.DoesNotContain("\"energyCost\"", json);
    }

    [Fact]
    public void TariffResponse_AnyTariff_PublishesPeakHoursForEveryDay()
    {
        var tariff = new CommercialTariff("Plus Energia", "MT2", "PEN", 0.2810m, 0.2395m, 58.40m,
            87.60m, 12.80m, 0.18m, DateTime.UtcNow);

        var response = EnergyResources.TariffResponse.From(tariff);

        Assert.Equal("18:00-23:00 every day", response.PeakHours);
        Assert.Contains("\"peak_hours\"", JsonSerializer.Serialize(response));
    }
}
