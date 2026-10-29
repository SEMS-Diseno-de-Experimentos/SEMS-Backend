using NSubstitute;
using Sems.Api.Modules.Energy.Application;
using Sems.Api.Modules.Energy.Domain.Model;
using Sems.Api.Modules.Energy.Domain.Repositories;
using Sems.Api.Modules.Energy.Domain.Services;
using Sems.Api.Shared.Errors;
using Sems.Api.Shared.Events;
using Xunit;

namespace Sems.Api.Tests.Energy;

/// <summary>
/// Casos de uso de energia con el proveedor de tarifas
/// (<see cref="IEnergyPricingProvider"/>) simulado.
/// </summary>
public class EnergyCommandServiceTests
{
    private readonly IEnergyMeterRepository _meters = Substitute.For<IEnergyMeterRepository>();
    private readonly IEnergyReadingRepository _readings = Substitute.For<IEnergyReadingRepository>();
    private readonly IEnergyPricingProvider _pricing = Substitute.For<IEnergyPricingProvider>();
    private readonly ISiteCatalog _sites = Substitute.For<ISiteCatalog>();
    private readonly IDomainEventBus _bus = Substitute.For<IDomainEventBus>();
    private readonly IMeterAllowance _cupos = Substitute.For<IMeterAllowance>();
    private readonly EnergyCommandService _service;

    public EnergyCommandServiceTests()
    {
        _pricing.CurrentTariff(Arg.Any<string?>()).Returns(call => new CommercialTariff(
            "Plus Energia", call.Arg<string?>()!, "PEN", 0.2810m, 0.2395m, 58.40m, 87.60m, 12.80m,
            0.18m, DateTime.UtcNow));
        _meters.SaveAsync(Arg.Any<EnergyMeter>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<EnergyMeter>());
        _readings.SaveAsync(Arg.Any<EnergyReading>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<EnergyReading>());

        // El local existe y esta vigente salvo que un test diga lo contrario:
        // casi ninguno habla de locales, y obligarlos a prepararlo esconderia
        // lo que si estan comprobando.
        _sites.SiteExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _sites.SiteIsActiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _sites.ZoneBelongsToSiteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Sin cupo declarado no se bloquea nada: lo contrario obligaria a cada
        // test a preparar un plan para hablar de otra cosa.
        _cupos.MeterAllowanceOfSiteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((IMeterAllowance.MeterAllowance?)null);
        _meters.FindBySiteIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<EnergyMeter>());

        _service = new EnergyCommandService(_meters, _readings,
            Substitute.For<IConsumptionAlertRepository>(), Substitute.For<IUserGoalRepository>(),
            _pricing, _sites, _bus, _cupos);
    }

    // ------------------------------------------------------- factura estimada

    [Theory]
    [InlineData("mt3", "MT3")]
    [InlineData(" MT2 ", "MT2")]
    [InlineData("bt5b", "BT5B")]
    public void EstimateBill_CategoryInAnyCase_UsesTheTariffOfTheNormalizedCategory(string requested,
        string expected)
    {
        var bill = _service.EstimateBill(requested, 6000m, 24000m, 110m, 120m);

        _pricing.Received(1).CurrentTariff(expected);
        Assert.True(bill.Total > 0);
    }

    [Theory]
    [InlineData(-1, 24000, 110)]
    [InlineData(6000, -1, 110)]
    [InlineData(6000, 24000, -1)]
    public void EstimateBill_NegativeConsumptionOrDemand_ThrowsValidationErrorWithoutCallingTheProvider(
        decimal kwhPeak, decimal kwhOffPeak, decimal demand)
    {
        var error = Assert.Throws<AppException>(() =>
            _service.EstimateBill("MT2", kwhPeak, kwhOffPeak, demand, 120m));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
        _pricing.DidNotReceiveWithAnyArgs().CurrentTariff(default);
    }

    [Fact]
    public void EstimateBill_ZeroContractedPower_ThrowsValidationError()
    {
        var error = Assert.Throws<AppException>(() =>
            _service.EstimateBill("MT2", 6000m, 24000m, 110m, 0m));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
    }

    [Theory]
    [InlineData("XYZ")]
    [InlineData("BT6")]
    [InlineData("")]
    [InlineData(null)]
    public void EstimateBill_UnknownOrEmptyCategory_ThrowsValidationErrorWithoutCallingTheProvider(
        string? category)
    {
        var error = Assert.Throws<AppException>(() =>
            _service.EstimateBill(category, 6000m, 24000m, 110m, 120m));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
        _pricing.DidNotReceiveWithAnyArgs().CurrentTariff(default);
    }

    [Fact]
    public void CurrentTariff_UnknownCategory_ThrowsValidationErrorWithoutCallingTheProvider()
    {
        var error = Assert.Throws<AppException>(() => _service.CurrentTariff("XYZ"));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
        _pricing.DidNotReceiveWithAnyArgs().CurrentTariff(default);
    }

    // ---------------------------------------------------------------- lecturas

    [Fact]
    public async Task RecordReadingAsync_ValidReading_PublishesReadingProcessed()
    {
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var meterId = Guid.NewGuid();

        var reading = await _service.RecordReadingAsync(userId.ToString(), meterId.ToString(),
            deviceId.ToString(), 95_000, 380, 150, 60, 2.5, null, null, null);

        await _readings.Received(1).SaveAsync(reading, Arg.Any<CancellationToken>());
        _bus.Received(1).Publish(Arg.Is<DomainEvents.ReadingProcessed>(e =>
            e.UserId == userId && e.DeviceId == deviceId && e.MeterId == meterId
            && e.ConsumptionKwh == 2.5m));
    }

    [Fact]
    public async Task RecordReadingAsync_SeventyHertz_ThrowsValidationErrorWithoutSavingOrPublishing()
    {
        var error = await Assert.ThrowsAsync<AppException>(() => _service.RecordReadingAsync(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), null, 95_000, 380, 150, 70, 2.5,
            null, null, null));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
        await _readings.DidNotReceive().SaveAsync(Arg.Any<EnergyReading>(), Arg.Any<CancellationToken>());
        _bus.DidNotReceive().Publish(Arg.Any<IDomainEvent>());
    }

    // --------------------------------------------------------------- medidores

    [Fact]
    public async Task RegisterMeterAsync_RepeatedSerialNumber_ThrowsConflict()
    {
        var existing = EnergyMeter.Register(Guid.NewGuid().ToString(), Guid.NewGuid(), null,
            "EOS-0001", null, null, null, null, null);
        _meters.FindBySerialAsync("EOS-0001", Arg.Any<CancellationToken>()).Returns(existing);

        var error = await Assert.ThrowsAsync<AppException>(() => _service.RegisterMeterAsync(
            Guid.NewGuid().ToString(), Guid.NewGuid(), null, " EOS-0001 ", "EOS-3F", "EOS", null,
            null, null));

        Assert.Equal(ErrorCode.CONFLICT, error.Code);
        await _meters.DidNotReceive().SaveAsync(Arg.Any<EnergyMeter>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterMeterAsync_NewSerialNumber_IsRegisteredActive()
    {
        var meter = await _service.RegisterMeterAsync(Guid.NewGuid().ToString(), Guid.NewGuid(),
            null, "EOS-0002", "EOS-3F", "EOS", "Main board", null, null);

        Assert.Equal(MeterStatus.active, meter.Status);
        Assert.Equal("1.0.0", meter.FirmwareVersion);
        await _meters.Received(1).SaveAsync(meter, Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------- cupo de medidores
    //
    // El catalogo vende "10 / 50 / ilimitado medidores por local" y hasta ahora
    // nadie lo miraba: un cliente del plan mas barato podia instalar los que
    // quisiera, con lo que el cupo dejaba de distinguir a un plan de otro.

    private static EnergyMeter Medidor(Guid local, MeterStatus estado = MeterStatus.active)
    {
        var medidor = EnergyMeter.Register("u-1", local, null, $"EOS-{Guid.NewGuid():N}", "V1",
            "EOS", "Tablero", "1.0.0", 100000);
        if (estado == MeterStatus.inactive)
        {
            medidor.Deactivate();
        }
        return medidor;
    }

    private void Cupo(Guid local, int medidores, string plan = "Básico") =>
        _cupos.MeterAllowanceOfSiteAsync(local, Arg.Any<CancellationToken>())
            .Returns(new IMeterAllowance.MeterAllowance(medidores, plan));

    private void YaInstalados(Guid local, params EnergyMeter[] medidores) =>
        _meters.FindBySiteIdAsync(local, Arg.Any<CancellationToken>()).Returns(medidores.ToList());

    [Fact]
    public async Task RegisterMeterAsync_WithRoomLeftInThePlan_Registers()
    {
        var local = Guid.NewGuid();
        Cupo(local, 2);
        YaInstalados(local, Medidor(local));

        var medidor = await _service.RegisterMeterAsync("u-1", local, null, "EOS-NUEVO", null, null,
            null, null, 100000);

        Assert.Equal(local, medidor.SiteId);
    }

    [Fact]
    public async Task RegisterMeterAsync_PlanQuotaReached_FailsSayingWhichPlanAndHowMany()
    {
        var local = Guid.NewGuid();
        Cupo(local, 2);
        YaInstalados(local, Medidor(local), Medidor(local));

        var error = await Assert.ThrowsAsync<AppException>(() => _service.RegisterMeterAsync(
            "u-1", local, null, "EOS-DEMAS", null, null, null, null, 100000));

        Assert.Equal(ErrorCode.CONFLICT, error.Code);
        Assert.Contains("Básico", error.Message);
        Assert.Contains("2 meter(s) per site", error.Message);
    }

    [Fact]
    public async Task RegisterMeterAsync_DeactivatedMetersDoNotTakeUpRoom()
    {
        // Es lo que promete US23: dar de baja un medidor retirado lo saca del
        // cupo. Si siguiera contando, el cliente tendria que cambiar de plan
        // por un aparato que ya no existe.
        var local = Guid.NewGuid();
        Cupo(local, 2);
        YaInstalados(local, Medidor(local), Medidor(local, MeterStatus.inactive));

        var medidor = await _service.RegisterMeterAsync("u-1", local, null, "EOS-REPUESTO", null,
            null, null, null, 100000);

        Assert.Equal(local, medidor.SiteId);
    }

    [Fact]
    public async Task RegisterMeterAsync_UnlimitedPlan_NeverBlocks()
    {
        var local = Guid.NewGuid();
        Cupo(local, int.MaxValue, "Enterprise");
        YaInstalados(local, Enumerable.Range(0, 50).Select(_ => Medidor(local)).ToArray());

        var medidor = await _service.RegisterMeterAsync("u-1", local, null, "EOS-51", null, null,
            null, null, 100000);

        Assert.Equal(local, medidor.SiteId);
    }

    [Fact]
    public async Task RegisterMeterAsync_SiteWithoutAPlanBehind_IsNotBlocked()
    {
        // Quien esta probando el producto tiene que poder instalar su primer
        // medidor antes de contratar nada.
        var local = Guid.NewGuid();
        YaInstalados(local, Medidor(local), Medidor(local));

        var medidor = await _service.RegisterMeterAsync("u-1", local, null, "EOS-PRUEBA", null,
            null, null, null, 100000);

        Assert.Equal(local, medidor.SiteId);
    }
}

