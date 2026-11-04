using NSubstitute;
using Sems.Api.Modules.Devices.Application;
using Sems.Api.Modules.Devices.Domain.Model;
using Sems.Api.Modules.Devices.Domain.Repositories;
using Sems.Api.Modules.Devices.Domain.Services;
using Sems.Api.Shared.Errors;
using Sems.Api.Shared.Events;
using Xunit;

namespace Sems.Api.Tests.Devices;

/// <summary>
/// Casos de uso de dispositivos con el puerto <see cref="ISiteDirectory"/>
/// simulado: Device Management no conoce el modulo de organizaciones.
/// </summary>
public class DeviceCommandServiceTests
{
    private static readonly Guid SiteId = Guid.NewGuid();
    private static readonly Guid ZoneId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly IDeviceRepository _devices = Substitute.For<IDeviceRepository>();
    private readonly ISiteDirectory _sites = Substitute.For<ISiteDirectory>();
    private readonly IDomainEventBus _bus = Substitute.For<IDomainEventBus>();
    private readonly DeviceCommandService _service;

    public DeviceCommandServiceTests()
    {
        _devices.SaveAsync(Arg.Any<Device>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Device>());
        _sites.SiteIsActiveAsync(SiteId, Arg.Any<CancellationToken>()).Returns(true);
        _sites.ZoneBelongsToSiteAsync(ZoneId, SiteId, Arg.Any<CancellationToken>()).Returns(true);

        _service = new DeviceCommandService(_devices, Substitute.For<IDeviceBindingRepository>(),
            Substitute.For<IDeviceConfigurationRepository>(), Substitute.For<IDeviceEventRepository>(),
            _sites, _bus);
    }

    private Task<Device> RegisterAsync(Guid siteId, Guid? zoneId, string code = "SM-0001") =>
        _service.RegisterAsync(code, UserId, siteId, zoneId, "Compressor submeter", "REFRIGERATION",
            "Schneider", "PM5110", "WIFI");

    private Device StoredDevice()
    {
        var device = Device.Register("SM-0009", UserId, SiteId, ZoneId, "Compressor submeter",
            "REFRIGERATION", null, null, ConnectionProtocol.WIFI);
        _devices.FindByIdAsync(device.DeviceId, Arg.Any<CancellationToken>()).Returns(device);
        return device;
    }

    // -------------------------------------------------------------------- alta

    [Fact]
    public async Task RegisterAsync_ActiveSiteAndOwnZone_SavesAndPublishesDeviceRegistered()
    {
        var device = await RegisterAsync(SiteId, ZoneId);

        await _devices.Received(1).SaveAsync(device, Arg.Any<CancellationToken>());
        _bus.Received(1).Publish(Arg.Is<DomainEvents.DeviceRegistered>(
            e => e.DeviceId == device.DeviceId && e.UserId == UserId));
        Assert.Equal(DeviceStatus.ACTIVE, device.Status);
    }

    [Fact]
    public async Task RegisterAsync_SiteMissingOrInactive_ThrowsNotFound()
    {
        var unknownSite = Guid.NewGuid();

        var error = await Assert.ThrowsAsync<AppException>(() => RegisterAsync(unknownSite, null));

        Assert.Equal(ErrorCode.NOT_FOUND, error.Code);
        await _devices.DidNotReceive().SaveAsync(Arg.Any<Device>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_ZoneOfAnotherSite_ThrowsValidationErrorAndPublishesNothing()
    {
        var foreignZone = Guid.NewGuid();

        var error = await Assert.ThrowsAsync<AppException>(() => RegisterAsync(SiteId, foreignZone));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
        _bus.DidNotReceive().Publish(Arg.Any<IDomainEvent>());
        await _devices.DidNotReceive().SaveAsync(Arg.Any<Device>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_RepeatedExternalCode_ThrowsConflictWithoutCheckingTheSite()
    {
        _devices.ExistsByExternalCodeAsync("SM-0001", Arg.Any<CancellationToken>()).Returns(true);

        var error = await Assert.ThrowsAsync<AppException>(() => RegisterAsync(SiteId, ZoneId, " SM-0001 "));

        Assert.Equal(ErrorCode.CONFLICT, error.Code);
        await _sites.DidNotReceive().SiteIsActiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------- baja

    [Fact]
    public async Task RemoveAsync_ExistingDevice_SavesItAsRemovedAndPublishesDeviceStatusUpdated()
    {
        var device = StoredDevice();

        await _service.RemoveAsync(device.DeviceId);

        Assert.Equal(DeviceStatus.REMOVED, device.Status);
        await _devices.Received(1).SaveAsync(device, Arg.Any<CancellationToken>());
        _bus.Received(1).Publish(Arg.Is<DomainEvents.DeviceStatusUpdated>(
            e => e.DeviceId == device.DeviceId && e.Status == "REMOVED"));
    }

    [Fact]
    public async Task RemoveAsync_UnknownDevice_ThrowsNotFound()
    {
        var error = await Assert.ThrowsAsync<AppException>(() => _service.RemoveAsync(Guid.NewGuid()));

        Assert.Equal(ErrorCode.NOT_FOUND, error.Code);
    }

    // ---------------------------------------------------------------- traslado

    [Fact]
    public async Task UpdateAsync_ZoneOfAnotherSite_ThrowsValidationError()
    {
        var device = StoredDevice();

        var error = await Assert.ThrowsAsync<AppException>(() => _service.UpdateAsync(device.DeviceId,
            "Compressor submeter", "REFRIGERATION", null, null, "WIFI", Guid.NewGuid()));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
        Assert.Equal(ZoneId, device.ZoneId);
    }

    [Fact]
    public async Task UpdateAsync_ZoneOfTheSameSite_MovesTheDevice()
    {
        var device = StoredDevice();
        var otherZone = Guid.NewGuid();
        _sites.ZoneBelongsToSiteAsync(otherZone, SiteId, Arg.Any<CancellationToken>()).Returns(true);

        var updated = await _service.UpdateAsync(device.DeviceId, "Compressor submeter",
            "REFRIGERATION", null, null, "WIFI", otherZone);

        Assert.Equal(otherZone, updated.ZoneId);
        Assert.Equal(SiteId, updated.SiteId);
    }
}
