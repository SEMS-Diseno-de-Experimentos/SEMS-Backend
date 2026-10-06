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
    private readonly IDomainEventBus _bus = Substitute.For<IDomainEventBus>();
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

        _service = new EnergyCommandService(_meters, _readings,
            Substitute.For<IConsumptionAlertRepository>(), Substitute.For<IUserGoalRepository>(),
            _pricing, _bus);
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
        var existing = EnergyMeter.Register(Guid.NewGuid().ToString(), "EOS-0001", null, null, null,
            null, null);
        _meters.FindBySerialAsync("EOS-0001", Arg.Any<CancellationToken>()).Returns(existing);

        var error = await Assert.ThrowsAsync<AppException>(() => _service.RegisterMeterAsync(
            Guid.NewGuid().ToString(), " EOS-0001 ", "EOS-3F", "EOS", null, null, null));

        Assert.Equal(ErrorCode.CONFLICT, error.Code);
        await _meters.DidNotReceive().SaveAsync(Arg.Any<EnergyMeter>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterMeterAsync_NewSerialNumber_IsRegisteredActive()
    {
        var meter = await _service.RegisterMeterAsync(Guid.NewGuid().ToString(), "EOS-0002", "EOS-3F",
            "EOS", "Main board", null, null);

        Assert.Equal(MeterStatus.active, meter.Status);
        Assert.Equal("1.0.0", meter.FirmwareVersion);
        await _meters.Received(1).SaveAsync(meter, Arg.Any<CancellationToken>());
    }
}
