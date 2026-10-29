using NSubstitute;
using Sems.Api.Modules.Energy.Application;
using Sems.Api.Modules.Energy.Domain.Model;
using Sems.Api.Modules.Energy.Domain.Repositories;
using Sems.Api.Modules.Energy.Domain.Services;
using Sems.Api.Modules.Energy.Infrastructure;
using Sems.Api.Shared.Errors;
using Xunit;

namespace Sems.Api.Tests.Energy;

/// <summary>
/// Reparto del consumo por zona, entre locales y en el instante.
/// </summary>
/// <remarks>
/// <para>Las dos primeras preguntas se contestan igual pero no significan lo
/// mismo. Dentro de un local interesa <i>donde</i> se gasta, y ahi el reparto
/// tiene que sumar cien: un medidor sin zona que se omita convierte el reparto
/// en una mentira. Entre locales interesa <i>cual</i> gasta de mas, y ahi el
/// total engana, porque un local grande consume mas sin que eso diga nada; por
/// eso se da tambien el consumo por kW contratado.</para>
/// </remarks>
public class ConsumptionBreakdownServiceTests
{
    private readonly IEnergyMeterRepository _meters = Substitute.For<IEnergyMeterRepository>();
    private readonly IEnergyReadingRepository _readings = Substitute.For<IEnergyReadingRepository>();
    private readonly ISiteCatalog _sites = Substitute.For<ISiteCatalog>();
    private readonly ConsumptionBreakdownService _service;

    private static readonly Guid Local = Guid.NewGuid();
    private static readonly Guid Organizacion = Guid.NewGuid();
    private static readonly Guid Frio = Guid.NewGuid();
    private static readonly Guid Sala = Guid.NewGuid();
    private const string Usuario = "11111111-1111-1111-1111-111111111111";

    public ConsumptionBreakdownServiceTests()
    {
        _service = new ConsumptionBreakdownService(_meters, _readings,
            new MockPlusEnergiaAdapter(), _sites);

        _sites.SiteSummaryAsync(Local, Arg.Any<CancellationToken>())
            .Returns(new ISiteCatalog.SiteSummary(Local, "T-001", "Sede Miraflores", 120m, "MT2",
                false));
        _sites.ActiveZonesOfAsync(Local, Arg.Any<CancellationToken>())
            .Returns(new List<ISiteCatalog.ZoneSummary>
            {
                new(Frio, "Camaras de frio", "COLD_STORAGE"),
                new(Sala, "Sala de ventas", "SALES_FLOOR")
            });
    }

    /// <summary>Un medidor del local, opcionalmente en una zona.</summary>
    private static EnergyMeter Medidor(Guid? zona) =>
        EnergyMeter.Register(Usuario, Local, zona, $"EOS-{Guid.NewGuid():N}", "V1", "EOS",
            "Tablero", "1.0.0", 100000);

    /// <summary>Lectura fuera de punta: las 10:00 de Peru son las 15:00 UTC.</summary>
    private static EnergyReading Lectura(EnergyMeter medidor, double kwh, double potenciaW = 0,
        int horaUtc = 15) =>
        EnergyReading.Record(Usuario, medidor.Id.ToString(), null,
            new PowerReading(potenciaW, 220, 10, 60, kwh),
            new DateTime(2026, 10, 20, horaUtc, 0, 0, DateTimeKind.Utc), "real_time", "single");

    private void Medidores(params EnergyMeter[] medidores) =>
        _meters.FindBySiteIdAsync(Local, Arg.Any<CancellationToken>())
            .Returns(medidores.ToList());

    private void Lecturas(params EnergyReading[] lecturas) =>
        _readings.FindByMetersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>()).Returns(lecturas.ToList());

    // ------------------------------------------------------- reparto por zona

    [Fact]
    public async Task ByZoneAsync_UnknownSite_Fails()
    {
        var desconocido = Guid.NewGuid();
        _sites.SiteSummaryAsync(desconocido, Arg.Any<CancellationToken>())
            .Returns((ISiteCatalog.SiteSummary?)null);

        var error = await Assert.ThrowsAsync<AppException>(
            () => _service.ByZoneAsync(desconocido, 30));

        Assert.Equal(ErrorCode.NOT_FOUND, error.Code);
    }

    [Fact]
    public async Task ByZoneAsync_TwoZones_SplitsTheConsumptionAndTheSharesAddUpToAHundred()
    {
        var frio = Medidor(Frio);
        var sala = Medidor(Sala);
        Medidores(frio, sala);
        Lecturas(Lectura(frio, 300), Lectura(sala, 100));

        var reparto = await _service.ByZoneAsync(Local, 30);

        Assert.Equal(400, reparto.TotalKwh);
        Assert.Equal("Sede Miraflores", reparto.SiteName);
        // Ordenado de mayor a menor: donde intervenir primero va arriba.
        Assert.Equal(new[] { "Camaras de frio", "Sala de ventas" },
            reparto.Zones.Select(z => z.ZoneName));
        Assert.Equal(75, reparto.Zones[0].SharePercent);
        Assert.Equal(25, reparto.Zones[1].SharePercent);
        Assert.Equal(100, reparto.Zones.Sum(z => z.SharePercent) + reparto.Unassigned.SharePercent);
    }

    [Fact]
    public async Task ByZoneAsync_MeterWithoutAZone_ItsConsumptionIsReportedAsUnassigned()
    {
        // Esconderlo haria que los porcentajes no sumaran cien y el reparto
        // mentiria sobre donde se gasta.
        var frio = Medidor(Frio);
        var sinZona = Medidor(null);
        Medidores(frio, sinZona);
        Lecturas(Lectura(frio, 150), Lectura(sinZona, 50));

        var reparto = await _service.ByZoneAsync(Local, 30);

        Assert.Equal(50, reparto.Unassigned.Kwh);
        Assert.Equal(25, reparto.Unassigned.SharePercent);
        Assert.Equal(1, reparto.Unassigned.MeterCount);
        Assert.Null(reparto.Unassigned.ZoneId);
    }

    [Fact]
    public async Task ByZoneAsync_ZoneWithoutReadings_IsListedAtZeroAndNotOmitted()
    {
        var frio = Medidor(Frio);
        Medidores(frio);
        Lecturas(Lectura(frio, 200));

        var reparto = await _service.ByZoneAsync(Local, 30);

        var sala = Assert.Single(reparto.Zones, z => z.ZoneName == "Sala de ventas");
        Assert.Equal(0, sala.Kwh);
        Assert.Equal(0, sala.MeterCount);
    }

    [Fact]
    public async Task ByZoneAsync_PeakAndOffPeakReadings_PricesEachBandWithItsOwnRate()
    {
        // Las 19:00 de Peru son las 00:00 UTC del dia siguiente: franja punta.
        var frio = Medidor(Frio);
        Medidores(frio);
        Lecturas(Lectura(frio, 100), Lectura(frio, 100, horaUtc: 0));

        var reparto = await _service.ByZoneAsync(Local, 30);

        var zona = Assert.Single(reparto.Zones, z => z.ZoneName == "Camaras de frio");
        Assert.Equal("PEN", reparto.Currency);
        // 100 kWh a 0,2810 en punta y 100 a 0,2395 fuera.
        Assert.Equal(100 * 0.2810m + 100 * 0.2395m, zona.EstimatedEnergyCost);
    }

    [Fact]
    public async Task ByZoneAsync_NoMeters_ReportsAnEmptyBreakdownWithoutFailing()
    {
        Medidores();
        _readings.FindByMetersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>()).Returns(new List<EnergyReading>());

        var reparto = await _service.ByZoneAsync(Local, 30);

        Assert.Equal(0, reparto.TotalKwh);
        Assert.All(reparto.Zones, z => Assert.Equal(0, z.Kwh));
    }

    [Theory]
    [InlineData(0, ConsumptionBreakdownService.DiasPorDefecto)]
    [InlineData(-5, ConsumptionBreakdownService.DiasPorDefecto)]
    [InlineData(7, 7)]
    [InlineData(5000, 365)]
    public async Task ByZoneAsync_AnyWindow_IsClampedToSomethingSensible(int pedidos, int esperados)
    {
        Medidores();
        _readings.FindByMetersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>()).Returns(new List<EnergyReading>());

        Assert.Equal(esperados, (await _service.ByZoneAsync(Local, pedidos)).Days);
    }

    // -------------------------------------------------- comparacion de locales

    [Fact]
    public async Task BySiteAsync_SitesOfDifferentSize_ComparesByConsumptionPerContractedKw()
    {
        // El local grande consume el doble, pero rinde igual por kW contratado:
        // el total solo, sin normalizar, haria pensar que gasta de mas.
        var grande = Guid.NewGuid();
        var pequeno = Guid.NewGuid();
        _sites.ActiveSitesOfAsync(Organizacion, Arg.Any<CancellationToken>())
            .Returns(new List<ISiteCatalog.SiteSummary>
            {
                new(grande, "T-001", "Sede grande", 200m, "MT2", false),
                new(pequeno, "T-002", "Sede pequena", 100m, "MT2", false)
            });

        var medidorGrande = EnergyMeter.Register(Usuario, grande, null, "EOS-G", "V1", "EOS", "T",
            "1.0.0", 100000);
        var medidorPequeno = EnergyMeter.Register(Usuario, pequeno, null, "EOS-P", "V1", "EOS", "T",
            "1.0.0", 100000);
        _meters.FindBySiteIdAsync(grande, Arg.Any<CancellationToken>())
            .Returns(new List<EnergyMeter> { medidorGrande });
        _meters.FindBySiteIdAsync(pequeno, Arg.Any<CancellationToken>())
            .Returns(new List<EnergyMeter> { medidorPequeno });
        _readings.FindByMetersAsync(
                Arg.Is<IReadOnlyCollection<string>>(ids => ids.Contains(medidorGrande.Id.ToString())),
                Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<EnergyReading> { Lectura(medidorGrande, 2000, 150_000) });
        _readings.FindByMetersAsync(
                Arg.Is<IReadOnlyCollection<string>>(ids => ids.Contains(medidorPequeno.Id.ToString())),
                Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<EnergyReading> { Lectura(medidorPequeno, 1000, 70_000) });

        var comparacion = await _service.BySiteAsync(Organizacion, 30);

        Assert.Equal(3000, comparacion.TotalKwh);
        Assert.Equal(new[] { "Sede grande", "Sede pequena" },
            comparacion.Sites.Select(s => s.SiteName));
        Assert.Equal(10, comparacion.Sites[0].KwhPerContractedKw);
        Assert.Equal(10, comparacion.Sites[1].KwhPerContractedKw);
        Assert.Equal(150, comparacion.Sites[0].PeakDemandKw);
        Assert.Equal(66.7, comparacion.Sites[0].SharePercent);
    }

    [Fact]
    public async Task BySiteAsync_OrganizationWithoutSites_ReportsAnEmptyComparison()
    {
        _sites.ActiveSitesOfAsync(Organizacion, Arg.Any<CancellationToken>())
            .Returns(new List<ISiteCatalog.SiteSummary>());

        var comparacion = await _service.BySiteAsync(Organizacion, 30);

        Assert.Empty(comparacion.Sites);
        Assert.Equal(0, comparacion.TotalKwh);
        Assert.Equal("PEN", comparacion.Currency);
    }

    // ------------------------------------------------------- consumo actual

    [Fact]
    public async Task CurrentAsync_SeveralMeters_AddsTheirLatestPowerAndItsOccupancy()
    {
        var frio = Medidor(Frio);
        var sala = Medidor(Sala);
        Medidores(frio, sala);
        _readings.FindLatestByMeterAsync(frio.Id.ToString(), Arg.Any<CancellationToken>())
            .Returns(Lectura(frio, 10, 80_000));
        _readings.FindLatestByMeterAsync(sala.Id.ToString(), Arg.Any<CancellationToken>())
            .Returns(Lectura(sala, 5, 22_000));

        var actual = await _service.CurrentAsync(Local);

        Assert.Equal(102, actual.PowerKw);
        Assert.Equal(120m, actual.ContractedPowerKw);
        // 102 de 120 kW: justo el umbral del 85 % con el que avisa la regla.
        Assert.Equal(85, actual.OccupancyPercent);
        Assert.Equal(2, actual.MeterCount);
    }

    [Fact]
    public async Task CurrentAsync_MeterThatNeverReported_IsCountedButAddsNoPower()
    {
        var frio = Medidor(Frio);
        var mudo = Medidor(Sala);
        Medidores(frio, mudo);
        _readings.FindLatestByMeterAsync(frio.Id.ToString(), Arg.Any<CancellationToken>())
            .Returns(Lectura(frio, 10, 60_000));
        _readings.FindLatestByMeterAsync(mudo.Id.ToString(), Arg.Any<CancellationToken>())
            .Returns((EnergyReading?)null);

        var actual = await _service.CurrentAsync(Local);

        Assert.Equal(60, actual.PowerKw);
        Assert.Equal(2, actual.MeterCount);
        Assert.NotNull(actual.MeasuredAt);
    }

    [Fact]
    public async Task CurrentAsync_UnknownSite_Fails()
    {
        var desconocido = Guid.NewGuid();
        _sites.SiteSummaryAsync(desconocido, Arg.Any<CancellationToken>())
            .Returns((ISiteCatalog.SiteSummary?)null);

        var error = await Assert.ThrowsAsync<AppException>(() => _service.CurrentAsync(desconocido));

        Assert.Equal(ErrorCode.NOT_FOUND, error.Code);
    }
}
