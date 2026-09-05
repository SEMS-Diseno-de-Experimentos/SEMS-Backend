using System.Text.Json;
using Sems.Api.Modules.Energy.Interfaces;
using Xunit;

namespace Sems.Api.Tests;

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
    public void ReadingResponse_se_serializa_en_snake_case()
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
    public void CreateReadingRequest_se_deserializa_desde_snake_case()
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
    public void ConsumptionResponse_conserva_los_nombres_del_dashboard()
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
}
