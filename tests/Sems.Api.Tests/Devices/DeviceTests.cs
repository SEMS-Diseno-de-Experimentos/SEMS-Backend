using Sems.Api.Modules.Devices.Domain.Model;
using Sems.Api.Shared.Errors;
using Xunit;

namespace Sems.Api.Tests.Devices;

/// <summary>
/// El agregado <see cref="Device"/> y su maquina de estados.
/// </summary>
public class DeviceTests
{
    private static readonly Guid SiteId = Guid.NewGuid();
    private static readonly Guid ZoneId = Guid.NewGuid();

    private static Device ValidDevice() => Device.Register("SM-0001", Guid.NewGuid(), SiteId, ZoneId,
        "Compressor submeter", "REFRIGERATION", "Schneider", "PM5110", ConnectionProtocol.WIFI);

    // -------------------------------------------------------------------- alta

    [Fact]
    public void Register_ValidData_StartsActiveInItsSite()
    {
        var device = ValidDevice();

        Assert.Equal(DeviceStatus.ACTIVE, device.Status);
        Assert.Equal(SiteId, device.SiteId);
        Assert.Equal(ZoneId, device.ZoneId);
        Assert.Equal("SM-0001", device.ExternalDeviceCode);
    }

    [Fact]
    public void Register_WithoutZone_IsAllowed()
    {
        var device = Device.Register("SM-0002", Guid.NewGuid(), SiteId, null, "Main board",
            "OTHER", null, null, ConnectionProtocol.WIFI);

        Assert.Null(device.ZoneId);
    }

    public static TheoryData<string?, bool, string?, string?> MissingRequiredData => new()
    {
        // codigo externo, con local, nombre, tipo
        { "SM-0001", false, "Compressor submeter", "REFRIGERATION" },
        { null, true, "Compressor submeter", "REFRIGERATION" },
        { "   ", true, "Compressor submeter", "REFRIGERATION" },
        { "SM-0001", true, null, "REFRIGERATION" },
        { "SM-0001", true, " ", "REFRIGERATION" },
        { "SM-0001", true, "Compressor submeter", null },
        { "SM-0001", true, "Compressor submeter", "" }
    };

    [Theory]
    [MemberData(nameof(MissingRequiredData))]
    public void Register_WithoutSiteCodeNameOrType_ThrowsValidationError(string? code, bool withSite,
        string? name, string? type)
    {
        var error = Assert.Throws<AppException>(() => Device.Register(code, Guid.NewGuid(),
            withSite ? SiteId : Guid.Empty, null, name, type, null, null, ConnectionProtocol.WIFI));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
    }

    // -------------------------------------------------------------------- baja

    [Fact]
    public void Remove_ActiveDevice_BecomesRemovedAndKeepsItsData()
    {
        var device = ValidDevice();

        device.Remove();

        Assert.Equal(DeviceStatus.REMOVED, device.Status);
        Assert.True(device.IsRemoved);
        Assert.Equal("SM-0001", device.ExternalDeviceCode);
        Assert.Equal(SiteId, device.SiteId);
        Assert.Equal("Compressor submeter", device.DeviceName);
    }

    [Fact]
    public void Remove_AlreadyRemovedDevice_ThrowsConflict()
    {
        var device = ValidDevice();
        device.Remove();

        var error = Assert.Throws<AppException>(() => device.Remove());

        Assert.Equal(ErrorCode.CONFLICT, error.Code);
    }

    [Fact]
    public void UpdateDetails_RemovedDevice_ThrowsConflict()
    {
        var device = ValidDevice();
        device.Remove();

        var error = Assert.Throws<AppException>(() => device.UpdateDetails("New name", "OTHER", null,
            null, ConnectionProtocol.WIFI, null));

        Assert.Equal(ErrorCode.CONFLICT, error.Code);
    }

    [Fact]
    public void EnsureCanBeBound_RemovedDevice_ThrowsConflict()
    {
        var device = ValidDevice();
        device.Remove();

        Assert.Equal(ErrorCode.CONFLICT, Assert.Throws<AppException>(device.EnsureCanBeBound).Code);
    }

    [Fact]
    public void EnsureCanUpdateConfiguration_RemovedDevice_ThrowsConflict()
    {
        var device = ValidDevice();
        device.Remove();

        Assert.Equal(ErrorCode.CONFLICT,
            Assert.Throws<AppException>(device.EnsureCanUpdateConfiguration).Code);
    }

    // ------------------------------------------------------- maquina de estados

    [Theory]
    [InlineData(DeviceStatus.ACTIVE)]
    [InlineData(DeviceStatus.INACTIVE)]
    [InlineData(DeviceStatus.DISCONNECTED)]
    [InlineData(DeviceStatus.MAINTENANCE)]
    [InlineData(DeviceStatus.REMOVED)]
    public void CanTransitionTo_FromRemoved_ReturnsFalseBecauseRemovedIsTerminal(DeviceStatus next)
    {
        Assert.False(DeviceStatus.REMOVED.CanTransitionTo(next));
    }

    [Theory]
    [InlineData(DeviceStatus.ACTIVE, DeviceStatus.MAINTENANCE)]
    [InlineData(DeviceStatus.MAINTENANCE, DeviceStatus.ACTIVE)]
    [InlineData(DeviceStatus.INACTIVE, DeviceStatus.REMOVED)]
    [InlineData(DeviceStatus.DISCONNECTED, DeviceStatus.INACTIVE)]
    public void CanTransitionTo_BetweenLiveStatuses_ReturnsTrue(DeviceStatus current, DeviceStatus next)
    {
        Assert.True(current.CanTransitionTo(next));
    }

    [Fact]
    public void ToDeviceStatus_UnknownStatus_ThrowsValidationError()
    {
        Assert.Equal(ErrorCode.VALIDATION_ERROR,
            Assert.Throws<AppException>(() => DeviceEnums.ToDeviceStatus("BROKEN")).Code);
    }

    // ---------------------------------------------------------------- traslado

    [Fact]
    public void UpdateDetails_NewZone_IsAllowedAndTheSiteDoesNotChange()
    {
        var device = ValidDevice();
        var newZone = Guid.NewGuid();

        device.UpdateDetails("Compressor submeter", "REFRIGERATION", "Schneider", "PM5110",
            ConnectionProtocol.ZIGBEE, newZone);

        Assert.Equal(newZone, device.ZoneId);
        Assert.Equal(SiteId, device.SiteId);
        Assert.Equal(ConnectionProtocol.ZIGBEE, device.ConnectionProtocol);
    }
}
