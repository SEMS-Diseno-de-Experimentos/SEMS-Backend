using Microsoft.AspNetCore.Mvc;
using Sems.Api.Modules.Devices.Application;
using Sems.Api.Shared.Http;
using Sems.Api.Shared.Security;
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
    private readonly IAccessPolicy _access;

    public DeviceController(DeviceCommandService commands, DeviceQueryService queries,
        IAccessPolicy access)
    {
        _commands = commands;
        _queries = queries;
        _access = access;
    }

    /// <summary>
    /// El equipo es del llamante, o esta en un local que alcanza.
    /// </summary>
    /// <remarks>
    /// Un equipo si guarda el local donde esta instalado, asi que aqui el
    /// supervisor del local entra aunque el aparato lo registrara un operario.
    /// </remarks>
    private async Task DebeAlcanzarElEquipoAsync(Guid deviceId)
    {
        var equipo = await _queries.DeviceByIdAsync(deviceId);
        await _access.MustOwnOrReachSiteAsync(User.CallerId(), equipo.UserId, equipo.SiteId);
    }

    /// <summary>Registers a new device.</summary>
    [HttpPost("devices")]
    public async Task<ActionResult<DeviceResource>> Create([FromBody] CreateDeviceRequest request)
    {
        var device = await _commands.RegisterAsync(request.ExternalDeviceCode,
            User.MustBeSelf(request.UserId), Guid.Parse(request.SiteId),
            ParseOptionalId(request.ZoneId),
            request.DeviceName, request.DeviceType,
            request.Brand, request.Model, request.ConnectionProtocol);
        return StatusCode(StatusCodes.Status201Created, DeviceResource.From(device));
    }

    /// <summary>Lists the caller's devices.</summary>
    /// <remarks>
    /// Antes devolvia los equipos de todos los usuarios: la ruta sin
    /// identificador era el camino mas corto para ver el inventario ajeno.
    /// </remarks>
    [HttpGet("devices")]
    public async Task<List<DeviceResource>> Mine() =>
        (await _queries.DevicesByUserAsync(User.CallerId())).Select(DeviceResource.From).ToList();

    /// <summary>Gets a device by its identifier.</summary>
    [HttpGet("devices/{deviceId:guid}")]
    public async Task<DeviceResource> ById(Guid deviceId)
    {
        var equipo = await _queries.DeviceByIdAsync(deviceId);
        await _access.MustOwnOrReachSiteAsync(User.CallerId(), equipo.UserId, equipo.SiteId);
        return DeviceResource.From(equipo);
    }

    /// <summary>Lists a user's devices.</summary>
    [HttpGet("users/{userId:guid}/devices")]
    public async Task<List<DeviceResource>> ByUser(Guid userId) =>
        (await _queries.DevicesByUserAsync(User.MustBeSelf(userId)))
        .Select(DeviceResource.From).ToList();

    /// <summary>Devices installed at a site.</summary>
    [HttpGet("sites/{siteId:guid}/devices")]
    public async Task<List<DeviceResource>> BySite(Guid siteId)
    {
        await _access.MustReachSiteAsync(User.CallerId(), siteId);
        return (await _queries.DevicesBySiteAsync(siteId)).Select(DeviceResource.From).ToList();
    }

    /// <summary>Devices in a specific zone.</summary>
    [HttpGet("zones/{zoneId:guid}/devices")]
    public async Task<List<DeviceResource>> ByZone(Guid zoneId)
    {
        await _access.MustReachZoneAsync(User.CallerId(), zoneId);
        return (await _queries.DevicesByZoneAsync(zoneId)).Select(DeviceResource.From).ToList();
    }

    /// <summary>Updates the editable details of a device.</summary>
    [HttpPut("devices/{deviceId:guid}")]
    public async Task<DeviceResource> Update(Guid deviceId, [FromBody] UpdateDeviceRequest request)
    {
        await DebeAlcanzarElEquipoAsync(deviceId);
        return DeviceResource.From(await _commands.UpdateAsync(deviceId, request.DeviceName,
            request.DeviceType, request.Brand, request.Model, request.ConnectionProtocol,
            ParseOptionalId(request.ZoneId)));
    }

    /// <summary>Converts an optional identifier from the body into a Guid.</summary>
    private static Guid? ParseOptionalId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Guid.Parse(value);

    /// <summary>Changes the status of a device.</summary>
    [HttpPatch("devices/{deviceId:guid}/status")]
    public async Task<DeviceResource> ChangeStatus(Guid deviceId,
        [FromBody] UpdateDeviceStatusRequest request)
    {
        await DebeAlcanzarElEquipoAsync(deviceId);
        return DeviceResource.From(await _commands.ChangeStatusAsync(deviceId, request.Status));
    }

    /// <summary>Deletes a device (soft delete).</summary>
    [HttpDelete("devices/{deviceId:guid}")]
    public async Task<IActionResult> Remove(Guid deviceId)
    {
        await DebeAlcanzarElEquipoAsync(deviceId);
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

    private readonly IAccessPolicy _access;

    public DeviceBindingController(DeviceCommandService commands, DeviceQueryService queries,
        IAccessPolicy access)
    {
        _commands = commands;
        _queries = queries;
        _access = access;
    }

    /// <summary>El equipo es del llamante, o esta en un local que alcanza.</summary>
    private async Task DebeAlcanzarElEquipoAsync(Guid deviceId)
    {
        var equipo = await _queries.DeviceByIdAsync(deviceId);
        await _access.MustOwnOrReachSiteAsync(User.CallerId(), equipo.UserId, equipo.SiteId);
    }


    /// <summary>Links a device to a user.</summary>
    [HttpPost("devices/{deviceId:guid}/bindings")]
    public async Task<ActionResult<DeviceBindingResource>> Bind(Guid deviceId,
        [FromBody] CreateBindingRequest request)
    {
        Guid? siteId = string.IsNullOrWhiteSpace(request.SiteId)
            ? null : Guid.Parse(request.SiteId);

        // El usuario del cuerpo es a quien se le asigna el equipo, no quien
        // llama: un supervisor asigna equipos a sus operarios. Lo que hay que
        // comprobar es que el equipo sea alcanzable por quien asigna.
        await DebeAlcanzarElEquipoAsync(deviceId);
        var binding = await _commands.BindAsync(deviceId, Guid.Parse(request.UserId), siteId);
        return StatusCode(StatusCodes.Status201Created, DeviceBindingResource.From(binding));
    }

    /// <summary>Lists the bindings of a device.</summary>
    [HttpGet("devices/{deviceId:guid}/bindings")]
    public async Task<List<DeviceBindingResource>> ByDevice(Guid deviceId)
    {
        await DebeAlcanzarElEquipoAsync(deviceId);
        return (await _queries.BindingsByDeviceAsync(deviceId))
            .Select(DeviceBindingResource.From).ToList();
    }

    /// <summary>Lists the bindings of a user.</summary>
    [HttpGet("users/{userId:guid}/bindings")]
    public async Task<List<DeviceBindingResource>> ByUser(Guid userId) =>
        (await _queries.BindingsByUserAsync(User.MustBeSelf(userId)))
        .Select(DeviceBindingResource.From).ToList();

    /// <summary>Unlinks a device.</summary>
    [HttpPatch("bindings/{bindingId:guid}/unlink")]
    public async Task<DeviceBindingResource> Unlink(Guid bindingId)
    {
        var vinculo = await _queries.BindingByIdAsync(bindingId);
        await _access.MustOwnOrReachSiteAsync(User.CallerId(), vinculo.UserId,
            vinculo.SiteId ?? Guid.Empty);
        return DeviceBindingResource.From(await _commands.UnbindAsync(bindingId));
    }
}

/// <summary>Named settings attached to a device.</summary>
[ApiController]
[Route("api/v1/device-management")]
[Tags("Device Configurations")]
public sealed class DeviceConfigurationController : ControllerBase
{
    private readonly DeviceCommandService _commands;
    private readonly DeviceQueryService _queries;

    private readonly IAccessPolicy _access;

    public DeviceConfigurationController(DeviceCommandService commands,
        DeviceQueryService queries, IAccessPolicy access)
    {
        _commands = commands;
        _queries = queries;
        _access = access;
    }

    /// <summary>El equipo es del llamante, o esta en un local que alcanza.</summary>
    private async Task DebeAlcanzarElEquipoAsync(Guid deviceId)
    {
        var equipo = await _queries.DeviceByIdAsync(deviceId);
        await _access.MustOwnOrReachSiteAsync(User.CallerId(), equipo.UserId, equipo.SiteId);
    }


    /// <summary>Creates or updates a device setting.</summary>
    [HttpPost("devices/{deviceId:guid}/configurations")]
    public async Task<ActionResult<DeviceConfigurationResource>> Upsert(Guid deviceId,
        [FromBody] CreateConfigurationRequest request)
    {
        await DebeAlcanzarElEquipoAsync(deviceId);
        var configuration = await _commands.UpsertConfigurationAsync(deviceId, request.ConfigKey,
            request.ConfigValue);
        return StatusCode(StatusCodes.Status201Created,
            DeviceConfigurationResource.From(configuration));
    }

    /// <summary>Lists the settings of a device.</summary>
    [HttpGet("devices/{deviceId:guid}/configurations")]
    public async Task<List<DeviceConfigurationResource>> ByDevice(Guid deviceId)
    {
        await DebeAlcanzarElEquipoAsync(deviceId);
        return (await _queries.ConfigurationsByDeviceAsync(deviceId))
            .Select(DeviceConfigurationResource.From).ToList();
    }

    /// <summary>Updates the value of a setting.</summary>
    [HttpPut("configurations/{configurationId:guid}")]
    public async Task<DeviceConfigurationResource> Update(Guid configurationId,
        [FromBody] UpdateConfigurationRequest request)
    {
        // El ajuste no guarda usuario ni local: los hereda del equipo al que
        // pertenece, que es por donde hay que preguntar.
        var ajuste = await _queries.ConfigurationByIdAsync(configurationId);
        await DebeAlcanzarElEquipoAsync(ajuste.DeviceId);
        return DeviceConfigurationResource.From(
            await _commands.UpdateConfigurationAsync(configurationId, request.ConfigValue));
    }
}

/// <summary>Log of what happened to each device.</summary>
[ApiController]
[Route("api/v1/device-management")]
[Tags("Device Events")]
public sealed class DeviceEventController : ControllerBase
{
    private readonly DeviceCommandService _commands;
    private readonly DeviceQueryService _queries;

    private readonly IAccessPolicy _access;

    public DeviceEventController(DeviceCommandService commands, DeviceQueryService queries,
        IAccessPolicy access)
    {
        _commands = commands;
        _queries = queries;
        _access = access;
    }

    /// <summary>El equipo es del llamante, o esta en un local que alcanza.</summary>
    private async Task DebeAlcanzarElEquipoAsync(Guid deviceId)
    {
        var equipo = await _queries.DeviceByIdAsync(deviceId);
        await _access.MustOwnOrReachSiteAsync(User.CallerId(), equipo.UserId, equipo.SiteId);
    }


    /// <summary>Records a device event.</summary>
    [HttpPost("devices/{deviceId:guid}/events")]
    public async Task<ActionResult<DeviceEventResource>> Create(Guid deviceId,
        [FromBody] CreateEventRequest request)
    {
        await DebeAlcanzarElEquipoAsync(deviceId);
        var deviceEvent = await _commands.RecordEventAsync(deviceId, request.EventType,
            request.Description, request.OccurredAt);
        return StatusCode(StatusCodes.Status201Created, DeviceEventResource.From(deviceEvent));
    }

    /// <summary>Lists a device's events, newest first.</summary>
    [HttpGet("devices/{deviceId:guid}/events")]
    public async Task<List<DeviceEventResource>> ByDevice(Guid deviceId)
    {
        await DebeAlcanzarElEquipoAsync(deviceId);
        return (await _queries.EventsByDeviceAsync(deviceId))
            .Select(DeviceEventResource.From).ToList();
    }
}
