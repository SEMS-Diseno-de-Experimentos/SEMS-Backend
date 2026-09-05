using Sems.Api.Modules.Energy.Domain.Model;

namespace Sems.Api.Modules.Energy.Domain.Repositories;

/// <summary>Puertos de salida del modulo de energia.</summary>
public interface IEnergyMeterRepository
{
    Task<EnergyMeter> SaveAsync(EnergyMeter meter, CancellationToken ct = default);
    Task<EnergyMeter?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<EnergyMeter?> FindBySerialAsync(string meterSerial, CancellationToken ct = default);
    Task<List<EnergyMeter>> FindByUserIdAsync(string userId, CancellationToken ct = default);
}

public interface IEnergyReadingRepository
{
    Task<EnergyReading> SaveAsync(EnergyReading reading, CancellationToken ct = default);
    Task<EnergyReading?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<EnergyReading>> FindByUserIdAsync(string userId, int limit, CancellationToken ct = default);
    Task<List<EnergyReading>> FindByDeviceIdAsync(string deviceId, int limit, int skip, CancellationToken ct = default);
    Task<List<EnergyReading>> FindByRangeAsync(string userId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<EnergyReading?> FindLatestByMeterAsync(string meterId, CancellationToken ct = default);
    Task<EnergyReading?> FindLatestByDeviceAsync(string deviceId, CancellationToken ct = default);
}

public interface IDeviceConsumptionRepository
{
    Task<DeviceConsumption> SaveAsync(DeviceConsumption consumption, CancellationToken ct = default);
    Task<DeviceConsumption?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<DeviceConsumption>> FindByUserIdAsync(string userId, CancellationToken ct = default);
    /// <summary>Los mayores consumidores del usuario, de mayor a menor.</summary>
    Task<List<DeviceConsumption>> FindTopByUserIdAsync(string userId, int limit, CancellationToken ct = default);
}

public interface IConsumptionAlertRepository
{
    Task<ConsumptionAlert> SaveAsync(ConsumptionAlert alert, CancellationToken ct = default);
    Task<ConsumptionAlert?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<ConsumptionAlert>> FindByUserIdAsync(string userId, CancellationToken ct = default);
    Task<List<ConsumptionAlert>> FindUnreadByUserIdAsync(string userId, CancellationToken ct = default);
}
