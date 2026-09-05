using Sems.Api.Modules.Energy.Domain.Model;
using Sems.Api.Modules.Energy.Domain.Repositories;
using Sems.Api.Modules.Energy.Domain.Services;
using Sems.Api.Shared.Errors;
using Sems.Api.Shared.Events;

namespace Sems.Api.Modules.Energy.Application;

/// <summary>Casos de uso que modifican el estado del modulo de energia.</summary>
public sealed class EnergyCommandService
{
    private readonly IEnergyMeterRepository _meters;
    private readonly IEnergyReadingRepository _readings;
    private readonly IConsumptionAlertRepository _alerts;
    private readonly IEnergyPricingProvider _pricing;
    private readonly IDomainEventBus _bus;

    public EnergyCommandService(IEnergyMeterRepository meters, IEnergyReadingRepository readings,
        IConsumptionAlertRepository alerts, IEnergyPricingProvider pricing, IDomainEventBus bus)
    {
        _meters = meters;
        _readings = readings;
        _alerts = alerts;
        _pricing = pricing;
        _bus = bus;
    }

    /// <summary>Vincular dos veces el mismo numero de serie es un conflicto.</summary>
    public async Task<EnergyMeter> RegisterMeterAsync(string? userId, string? meterSerial,
        string? model, string? brand, string? location, string? firmwareVersion,
        double? maxPowerWatts, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(meterSerial)
            && await _meters.FindBySerialAsync(meterSerial.Trim(), ct) is not null)
        {
            throw AppException.Conflict($"Meter '{meterSerial}' is already registered");
        }

        return await _meters.SaveAsync(EnergyMeter.Register(userId, meterSerial, model, brand,
            location, firmwareVersion, maxPowerWatts), ct);
    }

    public async Task<EnergyMeter> DeactivateMeterAsync(Guid meterId, CancellationToken ct = default)
    {
        var meter = await _meters.FindByIdAsync(meterId, ct)
                    ?? throw AppException.NotFound($"Meter '{meterId}' not found");
        meter.Deactivate();
        return await _meters.SaveAsync(meter, ct);
    }

    /// <summary>
    /// Registra una lectura y avisa al resto del sistema.
    ///
    /// <para>El evento <c>ReadingProcessed</c> es el que consumen analytics para
    /// rankings y proyecciones, y alerts para evaluar umbrales. Antes viajaba
    /// por el topic <c>energy.events</c>.</para>
    /// </summary>
    public async Task<EnergyReading> RecordReadingAsync(string? userId, string? meterId,
        string? deviceId, double powerWatts, double voltage, double current, double frequency,
        double energyKwh, DateTime? timestamp, string? readingType, string? phase,
        CancellationToken ct = default)
    {
        var measurement = new PowerReading(powerWatts, voltage, current, frequency, energyKwh);
        var reading = EnergyReading.Record(userId, meterId, deviceId, measurement, timestamp,
            readingType, phase);

        if (Guid.TryParse(userId, out var userGuid))
        {
            _bus.Publish(new DomainEvents.ReadingProcessed(userGuid,
                Guid.TryParse(deviceId, out var d) ? d : null,
                Guid.TryParse(meterId, out var m) ? m : null,
                (decimal)energyKwh, reading.Timestamp));
        }

        var saved = await _readings.SaveAsync(reading, ct);

        // El medidor deja constancia de que sigue vivo.
        if (Guid.TryParse(meterId, out var meterGuid))
        {
            var meter = await _meters.FindByIdAsync(meterGuid, ct);
            if (meter is not null)
            {
                meter.UpdateLastSeen();
                await _meters.SaveAsync(meter, ct);
            }
        }

        return saved;
    }

    public async Task<ConsumptionAlert> RaiseAlertAsync(string userId, string? deviceId,
        string? meterId, AlertType type, AlertSeverity severity, double thresholdValue,
        double actualValue, string? message, CancellationToken ct = default)
    {
        var alert = ConsumptionAlert.Raise(userId, deviceId, meterId, type, severity,
            thresholdValue, actualValue, message);

        if (Guid.TryParse(userId, out var userGuid))
        {
            _bus.Publish(new DomainEvents.AlertTriggered(userGuid, alert.Id, type.ToString(),
                severity.ToString(), message ?? string.Empty));
        }

        return await _alerts.SaveAsync(alert, ct);
    }

    public async Task<ConsumptionAlert> MarkAlertReadAsync(Guid alertId, CancellationToken ct = default)
    {
        var alert = await RequireAlertAsync(alertId, ct);
        alert.MarkAsRead();
        return await _alerts.SaveAsync(alert, ct);
    }

    public async Task<ConsumptionAlert> ResolveAlertAsync(Guid alertId, CancellationToken ct = default)
    {
        var alert = await RequireAlertAsync(alertId, ct);
        alert.Resolve();
        return await _alerts.SaveAsync(alert, ct);
    }

    public EnergyPrice CurrentPrice() => _pricing.CurrentPrice();

    private async Task<ConsumptionAlert> RequireAlertAsync(Guid alertId, CancellationToken ct) =>
        await _alerts.FindByIdAsync(alertId, ct)
        ?? throw AppException.NotFound($"Alert '{alertId}' not found");
}

/// <summary>Casos de uso de solo lectura del modulo de energia.</summary>
public sealed class EnergyQueryService
{
    private readonly IEnergyMeterRepository _meters;
    private readonly IEnergyReadingRepository _readings;
    private readonly IDeviceConsumptionRepository _consumptions;
    private readonly IConsumptionAlertRepository _alerts;

    public EnergyQueryService(IEnergyMeterRepository meters, IEnergyReadingRepository readings,
        IDeviceConsumptionRepository consumptions, IConsumptionAlertRepository alerts)
    {
        _meters = meters;
        _readings = readings;
        _consumptions = consumptions;
        _alerts = alerts;
    }

    public async Task<EnergyMeter> MeterByIdAsync(Guid meterId, CancellationToken ct = default) =>
        await _meters.FindByIdAsync(meterId, ct)
        ?? throw AppException.NotFound($"Meter '{meterId}' not found");

    public Task<List<EnergyMeter>> MetersByUserAsync(string userId, CancellationToken ct = default) =>
        _meters.FindByUserIdAsync(userId, ct);

    public async Task<EnergyReading> ReadingByIdAsync(Guid readingId, CancellationToken ct = default) =>
        await _readings.FindByIdAsync(readingId, ct)
        ?? throw AppException.NotFound($"Reading '{readingId}' not found");

    public Task<List<EnergyReading>> ReadingsByUserAsync(string userId, int limit,
        CancellationToken ct = default) => _readings.FindByUserIdAsync(userId, limit, ct);

    public Task<List<EnergyReading>> ReadingsByDeviceAsync(string deviceId, int limit, int skip,
        CancellationToken ct = default) => _readings.FindByDeviceIdAsync(deviceId, limit, skip, ct);

    public Task<List<EnergyReading>> ReadingsByRangeAsync(string userId, DateTime from, DateTime to,
        CancellationToken ct = default) => _readings.FindByRangeAsync(userId, from, to, ct);

    public async Task<EnergyReading> LatestByMeterAsync(string meterId, CancellationToken ct = default) =>
        await _readings.FindLatestByMeterAsync(meterId, ct)
        ?? throw AppException.NotFound($"No readings for meter '{meterId}'");

    public async Task<EnergyReading> LatestByDeviceAsync(string deviceId, CancellationToken ct = default) =>
        await _readings.FindLatestByDeviceAsync(deviceId, ct)
        ?? throw AppException.NotFound($"No readings found for device '{deviceId}'");

    public async Task<DeviceConsumption> ConsumptionByIdAsync(Guid id, CancellationToken ct = default) =>
        await _consumptions.FindByIdAsync(id, ct)
        ?? throw AppException.NotFound($"Consumption '{id}' not found");

    public Task<List<DeviceConsumption>> ConsumptionsByUserAsync(string userId,
        CancellationToken ct = default) => _consumptions.FindByUserIdAsync(userId, ct);

    public Task<List<DeviceConsumption>> TopConsumersByUserAsync(string userId, int limit,
        CancellationToken ct = default) => _consumptions.FindTopByUserIdAsync(userId, limit, ct);

    public async Task<ConsumptionAlert> AlertByIdAsync(Guid alertId, CancellationToken ct = default) =>
        await _alerts.FindByIdAsync(alertId, ct)
        ?? throw AppException.NotFound($"Alert '{alertId}' not found");

    public Task<List<ConsumptionAlert>> AlertsByUserAsync(string userId, CancellationToken ct = default) =>
        _alerts.FindByUserIdAsync(userId, ct);

    public Task<List<ConsumptionAlert>> UnreadAlertsByUserAsync(string userId,
        CancellationToken ct = default) => _alerts.FindUnreadByUserIdAsync(userId, ct);
}
