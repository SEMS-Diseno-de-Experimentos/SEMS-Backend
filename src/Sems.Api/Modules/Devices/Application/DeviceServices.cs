using Sems.Api.Modules.Devices.Domain.Model;
using Sems.Api.Modules.Devices.Domain.Repositories;
using Sems.Api.Modules.Devices.Domain.Services;
using Sems.Api.Shared.Errors;
using Sems.Api.Shared.Events;

namespace Sems.Api.Modules.Devices.Application;

/// <summary>
/// Casos de uso que modifican el estado del modulo de dispositivos.
///
/// <para>Orquesta agregados y repositorios, pero no contiene reglas de negocio:
/// esas viven en el dominio. Publica eventos al bus para que otros modulos
/// reaccionen, igual que el servicio original publicaba a los topics de
/// Kafka.</para>
/// </summary>
public sealed class DeviceCommandService
{
    private readonly IDeviceRepository _devices;
    private readonly IDeviceBindingRepository _bindings;
    private readonly IDeviceConfigurationRepository _configurations;
    private readonly IDeviceEventRepository _events;
    private readonly ISiteDirectory _sites;
    private readonly IDomainEventBus _bus;

    public DeviceCommandService(IDeviceRepository devices, IDeviceBindingRepository bindings,
        IDeviceConfigurationRepository configurations, IDeviceEventRepository events,
        ISiteDirectory sites, IDomainEventBus bus)
    {
        _devices = devices;
        _bindings = bindings;
        _configurations = configurations;
        _events = events;
        _sites = sites;
        _bus = bus;
    }

    public async Task<Device> RegisterAsync(string? externalCode, Guid userId, Guid siteId,
        Guid? zoneId, string? name, string? type, string? brand, string? model, string? protocol,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(externalCode)
            && await _devices.ExistsByExternalCodeAsync(externalCode.Trim(), ct))
        {
            throw AppException.Conflict("a device with that external_device_code already exists");
        }

        await ValidarUbicacionAsync(siteId, zoneId, ct);

        var device = Device.Register(externalCode, userId, siteId, zoneId, name, type, brand, model,
            DeviceEnums.ToConnectionProtocol(protocol));

        _bus.Publish(new DomainEvents.DeviceRegistered(device.UserId, device.DeviceId,
            device.DeviceName, device.DeviceType));

        return await _devices.SaveAsync(device, ct);
    }

    public async Task<Device> UpdateAsync(Guid deviceId, string? name, string? type, string? brand,
        string? model, string? protocol, Guid? zoneId, CancellationToken ct = default)
    {
        var device = await RequireAsync(deviceId, ct);

        // La zona nueva tiene que ser del mismo local. Sin comprobarlo se podria
        // mover un equipo al local de otra empresa pasando el identificador a
        // mano, y su consumo empezaria a contar en la factura equivocada.
        if (zoneId is not null && zoneId != Guid.Empty
            && !await _sites.ZoneBelongsToSiteAsync(zoneId.Value, device.SiteId, ct))
        {
            throw AppException.Validation("the zone does not belong to this device's site");
        }

        device.UpdateDetails(name, type, brand, model, DeviceEnums.ToConnectionProtocol(protocol),
            zoneId);
        return await _devices.SaveAsync(device, ct);
    }

    /// <summary>Comprueba que el local existe y que la zona es suya.</summary>
    private async Task ValidarUbicacionAsync(Guid siteId, Guid? zoneId, CancellationToken ct)
    {
        if (!await _sites.SiteIsActiveAsync(siteId, ct))
        {
            throw AppException.NotFound("site not found or not active");
        }

        if (zoneId is not null && zoneId != Guid.Empty
            && !await _sites.ZoneBelongsToSiteAsync(zoneId.Value, siteId, ct))
        {
            throw AppException.Validation("the zone does not belong to the given site");
        }
    }

    public async Task<Device> ChangeStatusAsync(Guid deviceId, string? status,
        CancellationToken ct = default)
    {
        var device = await RequireAsync(deviceId, ct);
        device.ChangeStatus(DeviceEnums.ToDeviceStatus(status));

        _bus.Publish(new DomainEvents.DeviceStatusUpdated(device.UserId, device.DeviceId,
            device.Status.ToString()));

        return await _devices.SaveAsync(device, ct);
    }

    /// <summary>Borrado logico: el dispositivo pasa a REMOVED y deja de listarse.</summary>
    public async Task RemoveAsync(Guid deviceId, CancellationToken ct = default)
    {
        var device = await RequireAsync(deviceId, ct);
        device.Remove();

        _bus.Publish(new DomainEvents.DeviceStatusUpdated(device.UserId, device.DeviceId,
            DeviceStatus.REMOVED.ToString()));

        await _devices.SaveAsync(device, ct);
    }

    public async Task<DeviceBinding> BindAsync(Guid deviceId, Guid userId, Guid? siteId,
        CancellationToken ct = default)
    {
        var device = await RequireAsync(deviceId, ct);
        device.EnsureCanBeBound();

        if (await _bindings.FindActiveByDeviceIdAsync(deviceId, ct) is not null)
        {
            throw AppException.Conflict("device already has an active binding");
        }

        var binding = DeviceBinding.Create(deviceId, userId, siteId);
        _bus.Publish(new DomainEvents.DeviceLinked(userId, deviceId, binding.BindingId));

        return await _bindings.SaveAsync(binding, ct);
    }

    public async Task<DeviceBinding> UnbindAsync(Guid bindingId, CancellationToken ct = default)
    {
        var binding = await _bindings.FindByIdAsync(bindingId, ct)
                      ?? throw AppException.NotFound("binding not found");

        binding.Unlink();
        _bus.Publish(new DomainEvents.DeviceUnlinked(binding.UserId, binding.DeviceId,
            binding.BindingId));

        return await _bindings.SaveAsync(binding, ct);
    }

    /// <summary>Si la clave ya existe para ese dispositivo, se actualiza su valor.</summary>
    public async Task<DeviceConfiguration> UpsertConfigurationAsync(Guid deviceId, string? key,
        string? value, CancellationToken ct = default)
    {
        var device = await RequireAsync(deviceId, ct);
        device.EnsureCanUpdateConfiguration();

        var existing = await _configurations.FindByDeviceIdAndKeyAsync(deviceId,
            key?.Trim() ?? string.Empty, ct);

        if (existing is not null)
        {
            existing.Update(value);
            return await _configurations.SaveAsync(existing, ct);
        }

        return await _configurations.SaveAsync(DeviceConfiguration.Create(deviceId, key, value), ct);
    }

    public async Task<DeviceConfiguration> UpdateConfigurationAsync(Guid configurationId,
        string? value, CancellationToken ct = default)
    {
        var configuration = await _configurations.FindByIdAsync(configurationId, ct)
                            ?? throw AppException.NotFound("configuration not found");

        configuration.Update(value);
        return await _configurations.SaveAsync(configuration, ct);
    }

    public async Task<DeviceEvent> RecordEventAsync(Guid deviceId, string? eventType,
        string? description, DateTime? occurredAt, CancellationToken ct = default)
    {
        await RequireAsync(deviceId, ct);
        return await _events.SaveAsync(
            DeviceEvent.Create(deviceId, eventType, description, occurredAt), ct);
    }

    private async Task<Device> RequireAsync(Guid deviceId, CancellationToken ct) =>
        await _devices.FindByIdAsync(deviceId, ct)
        ?? throw AppException.NotFound("device not found");
}

/// <summary>Casos de uso de solo lectura del modulo de dispositivos.</summary>
public sealed class DeviceQueryService
{
    private readonly IDeviceRepository _devices;
    private readonly IDeviceBindingRepository _bindings;
    private readonly IDeviceConfigurationRepository _configurations;
    private readonly IDeviceEventRepository _events;

    public DeviceQueryService(IDeviceRepository devices, IDeviceBindingRepository bindings,
        IDeviceConfigurationRepository configurations, IDeviceEventRepository events)
    {
        _devices = devices;
        _bindings = bindings;
        _configurations = configurations;
        _events = events;
    }

    public Task<List<Device>> AllDevicesAsync(CancellationToken ct = default) =>
        _devices.FindAllAsync(ct);

    public async Task<Device> DeviceByIdAsync(Guid deviceId, CancellationToken ct = default) =>
        await _devices.FindByIdAsync(deviceId, ct)
        ?? throw AppException.NotFound("device not found");

    public Task<List<Device>> DevicesByUserAsync(Guid userId, CancellationToken ct = default) =>
        _devices.FindByUserIdAsync(userId, ct);

    /// <summary>Dispositivos instalados en un local.</summary>
    public Task<List<Device>> DevicesBySiteAsync(Guid siteId, CancellationToken ct = default) =>
        _devices.FindBySiteIdAsync(siteId, ct);

    /// <summary>Dispositivos de una zona concreta.</summary>
    public Task<List<Device>> DevicesByZoneAsync(Guid zoneId, CancellationToken ct = default) =>
        _devices.FindByZoneIdAsync(zoneId, ct);

    public Task<List<DeviceBinding>> BindingsByDeviceAsync(Guid deviceId, CancellationToken ct = default) =>
        _bindings.FindByDeviceIdAsync(deviceId, ct);

    public Task<List<DeviceBinding>> BindingsByUserAsync(Guid userId, CancellationToken ct = default) =>
        _bindings.FindByUserIdAsync(userId, ct);

    public Task<List<DeviceConfiguration>> ConfigurationsByDeviceAsync(Guid deviceId,
        CancellationToken ct = default) =>
        _configurations.FindByDeviceIdAsync(deviceId, ct);

    public Task<List<DeviceEvent>> EventsByDeviceAsync(Guid deviceId, CancellationToken ct = default) =>
        _events.FindByDeviceIdAsync(deviceId, ct);
}
