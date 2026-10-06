using Microsoft.AspNetCore.Mvc;
using Sems.Api.Modules.Devices.Application;
using static Sems.Api.Modules.Devices.Interfaces.DeviceResources;

namespace Sems.Api.Modules.Devices.Interfaces;

/// <summary>
/// API REST del bounded context de dispositivos.
///
/// <para>Las rutas son identicas a las del microservicio original bajo
/// <c>/api/v1/device-management</c>. Mantenerlas iguales es lo que permite
/// cambiar el backend sin tocar el frontend.</para>
/// </summary>
[ApiController]
[Route("api/v1/device-management")]
[Tags("Device Management")]
public sealed class DeviceController : ControllerBase
{
    private readonly DeviceCommandService _commands;
    private readonly DeviceQueryService _queries;

    public DeviceController(DeviceCommandService commands, DeviceQueryService queries)
    {
        _commands = commands;
        _queries = queries;
    }

    /// <summary>Registers a new device.</summary>
    [HttpPost("devices")]
    public async Task<ActionResult<DeviceResource>> Create([FromBody] CreateDeviceRequest request)
    {
        var device = await _commands.RegisterAsync(request.ExternalDeviceCode,
            Guid.Parse(request.UserId), Guid.Parse(request.SiteId), ParseOptionalId(request.ZoneId),
            request.DeviceName, request.DeviceType,
            request.Brand, request.Model, request.ConnectionProtocol);
        return StatusCode(StatusCodes.Status201Created, DeviceResource.From(device));
    }

    /// <summary>Lists every device.</summary>
    [HttpGet("devices")]
    public async Task<List<DeviceResource>> List() =>
        (await _queries.AllDevicesAsync()).Select(DeviceResource.From).ToList();

    /// <summary>Gets a device by its identifier.</summary>
    [HttpGet("devices/{deviceId:guid}")]
    public async Task<DeviceResource> ById(Guid deviceId) =>
        DeviceResource.From(await _queries.DeviceByIdAsync(deviceId));

    /// <summary>Lists a user's devices.</summary>
    [HttpGet("users/{userId:guid}/devices")]
    public async Task<List<DeviceResource>> ByUser(Guid userId) =>
        (await _queries.DevicesByUserAsync(userId)).Select(DeviceResource.From).ToList();

    /// <summary>Devices installed at a site.</summary>
    [HttpGet("sites/{siteId:guid}/devices")]
    public async Task<List<DeviceResource>> BySite(Guid siteId) =>
        (await _queries.DevicesBySiteAsync(siteId)).Select(DeviceResource.From).ToList();

    /// <summary>Devices in a specific zone.</summary>
    [HttpGet("zones/{zoneId:guid}/devices")]
    public async Task<List<DeviceResource>> ByZone(Guid zoneId) =>
        (await _queries.DevicesByZoneAsync(zoneId)).Select(DeviceResource.From).ToList();

    /// <summary>Updates the editable details of a device.</summary>
    [HttpPut("devices/{deviceId:guid}")]
    public async Task<DeviceResource> Update(Guid deviceId, [FromBody] UpdateDeviceRequest request) =>
        DeviceResource.From(await _commands.UpdateAsync(deviceId, request.DeviceName,
            request.DeviceType, request.Brand, request.Model, request.ConnectionProtocol,
            ParseOptionalId(request.ZoneId)));

    /// <summary>Converts an optional identifier from the body into a Guid.</summary>
    private static Guid? ParseOptionalId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Guid.Parse(value);

    /// <summary>Changes the status of a device.</summary>
    [HttpPatch("devices/{deviceId:guid}/status")]
    public async Task<DeviceResource> ChangeStatus(Guid deviceId,
        [FromBody] UpdateDeviceStatusRequest request) =>
        DeviceResource.From(await _commands.ChangeStatusAsync(deviceId, request.Status));

    /// <summary>Deletes a device (soft delete).</summary>
    [HttpDelete("devices/{deviceId:guid}")]
    public async Task<IActionResult> Remove(Guid deviceId)
    {
        await _commands.RemoveAsync(deviceId);
        return NoContent();
    }
}

/// <summary>Binding of devices to the people who operate them.</summary>
[ApiController]
[Route("api/v1/device-management")]
[Tags("Device Bindings")]
public sealed class DeviceBindingController : ControllerBase
{
    private readonly DeviceCommandService _commands;
    private readonly DeviceQueryService _queries;

    public DeviceBindingController(DeviceCommandService commands, DeviceQueryService queries)
    {
        _commands = commands;
        _queries = queries;
    }

    /// <summary>Links a device to a user.</summary>
    [HttpPost("devices/{deviceId:guid}/bindings")]
    public async Task<ActionResult<DeviceBindingResource>> Bind(Guid deviceId,
        [FromBody] CreateBindingRequest request)
    {
        Guid? siteId = string.IsNullOrWhiteSpace(request.SiteId)
            ? null : Guid.Parse(request.SiteId);

        var binding = await _commands.BindAsync(deviceId, Guid.Parse(request.UserId), siteId);
        return StatusCode(StatusCodes.Status201Created, DeviceBindingResource.From(binding));
    }

    /// <summary>Lists the bindings of a device.</summary>
    [HttpGet("devices/{deviceId:guid}/bindings")]
    public async Task<List<DeviceBindingResource>> ByDevice(Guid deviceId) =>
        (await _queries.BindingsByDeviceAsync(deviceId)).Select(DeviceBindingResource.From).ToList();

    /// <summary>Lists the bindings of a user.</summary>
    [HttpGet("users/{userId:guid}/bindings")]
    public async Task<List<DeviceBindingResource>> ByUser(Guid userId) =>
        (await _queries.BindingsByUserAsync(userId)).Select(DeviceBindingResource.From).ToList();

    /// <summary>Unlinks a device.</summary>
    [HttpPatch("bindings/{bindingId:guid}/unlink")]
    public async Task<DeviceBindingResource> Unlink(Guid bindingId) =>
        DeviceBindingResource.From(await _commands.UnbindAsync(bindingId));
}

/// <summary>Named settings attached to a device.</summary>
[ApiController]
[Route("api/v1/device-management")]
[Tags("Device Configurations")]
public sealed class DeviceConfigurationController : ControllerBase
{
    private readonly DeviceCommandService _commands;
    private readonly DeviceQueryService _queries;

    public DeviceConfigurationController(DeviceCommandService commands, DeviceQueryService queries)
    {
        _commands = commands;
        _queries = queries;
    }

    /// <summary>Creates or updates a device setting.</summary>
    [HttpPost("devices/{deviceId:guid}/configurations")]
    public async Task<ActionResult<DeviceConfigurationResource>> Upsert(Guid deviceId,
        [FromBody] CreateConfigurationRequest request)
    {
        var configuration = await _commands.UpsertConfigurationAsync(deviceId, request.ConfigKey,
            request.ConfigValue);
        return StatusCode(StatusCodes.Status201Created,
            DeviceConfigurationResource.From(configuration));
    }

    /// <summary>Lists the settings of a device.</summary>
    [HttpGet("devices/{deviceId:guid}/configurations")]
    public async Task<List<DeviceConfigurationResource>> ByDevice(Guid deviceId) =>
        (await _queries.ConfigurationsByDeviceAsync(deviceId))
        .Select(DeviceConfigurationResource.From).ToList();

    /// <summary>Updates the value of a setting.</summary>
    [HttpPut("configurations/{configurationId:guid}")]
    public async Task<DeviceConfigurationResource> Update(Guid configurationId,
        [FromBody] UpdateConfigurationRequest request) =>
        DeviceConfigurationResource.From(
            await _commands.UpdateConfigurationAsync(configurationId, request.ConfigValue));
}

/// <summary>Log of what happened to each device.</summary>
[ApiController]
[Route("api/v1/device-management")]
[Tags("Device Events")]
public sealed class DeviceEventController : ControllerBase
{
    private readonly DeviceCommandService _commands;
    private readonly DeviceQueryService _queries;

    public DeviceEventController(DeviceCommandService commands, DeviceQueryService queries)
    {
        _commands = commands;
        _queries = queries;
    }

    /// <summary>Records a device event.</summary>
    [HttpPost("devices/{deviceId:guid}/events")]
    public async Task<ActionResult<DeviceEventResource>> Create(Guid deviceId,
        [FromBody] CreateEventRequest request)
    {
        var deviceEvent = await _commands.RecordEventAsync(deviceId, request.EventType,
            request.Description, request.OccurredAt);
        return StatusCode(StatusCodes.Status201Created, DeviceEventResource.From(deviceEvent));
    }

    /// <summary>Lists a device's events, newest first.</summary>
    [HttpGet("devices/{deviceId:guid}/events")]
    public async Task<List<DeviceEventResource>> ByDevice(Guid deviceId) =>
        (await _queries.EventsByDeviceAsync(deviceId)).Select(DeviceEventResource.From).ToList();
}
