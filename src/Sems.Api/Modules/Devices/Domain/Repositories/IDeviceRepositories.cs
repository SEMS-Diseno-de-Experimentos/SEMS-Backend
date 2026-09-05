using Sems.Api.Modules.Devices.Domain.Model;

namespace Sems.Api.Modules.Devices.Domain.Repositories;

/// <summary>
/// Puertos de salida del modulo de dispositivos.
///
/// <para>Declarados en el dominio y hablados en objetos de dominio: la capa de
/// aplicacion no sabe si detras hay EF Core, un documento o una llamada
/// remota.</para>
/// </summary>
public interface IDeviceRepository
{
    Task<Device> SaveAsync(Device device, CancellationToken ct = default);

    Task<Device?> FindByIdAsync(Guid deviceId, CancellationToken ct = default);

    Task<List<Device>> FindAllAsync(CancellationToken ct = default);

    Task<List<Device>> FindByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task<bool> ExistsByExternalCodeAsync(string externalDeviceCode, CancellationToken ct = default);
}

public interface IDeviceBindingRepository
{
    Task<DeviceBinding> SaveAsync(DeviceBinding binding, CancellationToken ct = default);

    Task<DeviceBinding?> FindByIdAsync(Guid bindingId, CancellationToken ct = default);

    Task<List<DeviceBinding>> FindByDeviceIdAsync(Guid deviceId, CancellationToken ct = default);

    Task<List<DeviceBinding>> FindByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Un dispositivo no puede tener dos vinculos activos a la vez.</summary>
    Task<DeviceBinding?> FindActiveByDeviceIdAsync(Guid deviceId, CancellationToken ct = default);
}

public interface IDeviceConfigurationRepository
{
    Task<DeviceConfiguration> SaveAsync(DeviceConfiguration configuration, CancellationToken ct = default);

    Task<DeviceConfiguration?> FindByIdAsync(Guid configurationId, CancellationToken ct = default);

    Task<List<DeviceConfiguration>> FindByDeviceIdAsync(Guid deviceId, CancellationToken ct = default);

    Task<DeviceConfiguration?> FindByDeviceIdAndKeyAsync(Guid deviceId, string configKey,
        CancellationToken ct = default);
}

public interface IDeviceEventRepository
{
    Task<DeviceEvent> SaveAsync(DeviceEvent deviceEvent, CancellationToken ct = default);

    /// <summary>Ordenados del mas reciente al mas antiguo.</summary>
    Task<List<DeviceEvent>> FindByDeviceIdAsync(Guid deviceId, CancellationToken ct = default);
}
