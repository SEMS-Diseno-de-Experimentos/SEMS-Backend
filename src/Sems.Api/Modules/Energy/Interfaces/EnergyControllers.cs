using Microsoft.AspNetCore.Mvc;
using Sems.Api.Modules.Energy.Application;
using Sems.Api.Modules.Energy.Domain.Model;
using System.Security.Claims;
using Sems.Api.Shared.Http;
using Sems.Api.Shared.Security;
using Sems.Api.Shared.Security;
using static Sems.Api.Modules.Energy.Interfaces.EnergyResources;

namespace Sems.Api.Modules.Energy.Interfaces;

/// <summary>Smart meters linked to each user.</summary>
[ApiController]
[Route("api/v1/energy-meters")]
[Tags("Energy Meters")]
public sealed class EnergyMeterController : ControllerBase
{
    private readonly EnergyCommandService _commands;
    private readonly EnergyQueryService _queries;
    private readonly IAccessPolicy _access;

    public EnergyMeterController(EnergyCommandService commands, EnergyQueryService queries,
        IAccessPolicy access)
    {
        _commands = commands;
        _queries = queries;
        _access = access;
    }

    /// <summary>Registers a new meter.</summary>
    [HttpPost]
    public async Task<ActionResult<MeterResponse>> Register([FromBody] RegisterMeterRequest request)
    {
        User.MustBeSelf(request.UserId);
        var meter = await _commands.RegisterMeterAsync(request.UserId, request.SiteId,
            request.ZoneId, request.MeterSerial, request.Model, request.Brand, request.Location,
            request.FirmwareVersion, request.MaxPowerWatts);
        return StatusCode(StatusCodes.Status201Created, MeterResponse.From(meter));
    }

    /// <summary>Lists a user's meters.</summary>
    /// <remarks>
    /// El identificador se comprueba contra el token pero se sigue pasando tal
    /// como llego: en este modulo el usuario se guarda como texto, y
    /// normalizarlo ahora dejaria de encontrar las filas escritas antes.
    /// </remarks>
    [HttpGet("user/{userId}")]
    public async Task<List<MeterResponse>> ByUser(string userId)
    {
        User.MustBeSelf(userId);
        return (await _queries.MetersByUserAsync(userId)).Select(MeterResponse.From).ToList();
    }

    /// <summary>Lists the meters installed in a site.</summary>
    /// <remarks>
    /// Esta ruta es del local y no de la persona, asi que quien supervisa el
    /// local ve sus medidores aunque los diera de alta un operario.
    /// </remarks>
    [HttpGet("site/{siteId:guid}")]
    public async Task<List<MeterResponse>> BySite(Guid siteId)
    {
        await _access.MustReachSiteAsync(User.CallerId(), siteId);
        return (await _queries.MetersBySiteAsync(siteId)).Select(MeterResponse.From).ToList();
    }

    /// <summary>Lists the meters of a zone.</summary>
    [HttpGet("zone/{zoneId:guid}")]
    public async Task<List<MeterResponse>> ByZone(Guid zoneId)
    {
        await _access.MustReachZoneAsync(User.CallerId(), zoneId);
        return (await _queries.MetersByZoneAsync(zoneId)).Select(MeterResponse.From).ToList();
    }

    /// <summary>Reassigns a meter to another zone of the same site.</summary>
    [HttpPatch("{meterId:guid}/zone")]
    public async Task<MeterResponse> ReassignZone(Guid meterId,
        [FromBody] ReassignMeterZoneRequest request)
    {
        await DebeAlcanzarElMedidorAsync(meterId);
        return MeterResponse.From(await _commands.ReassignMeterZoneAsync(meterId, request.ZoneId));
    }

    /// <summary>Deactivates a meter.</summary>
    [HttpPatch("{meterId:guid}/deactivate")]
    public async Task<MeterResponse> Deactivate(Guid meterId)
    {
        // Dar de baja el medidor de otro local corta la medicion de un
        // suministro que no es del llamante: es la escritura mas dañina que
        // habia abierta.
        await DebeAlcanzarElMedidorAsync(meterId);
        return MeterResponse.From(await _commands.DeactivateMeterAsync(meterId));
    }

    /// <summary>Gets a meter by its identifier.</summary>
    [HttpGet("{meterId:guid}")]
    public async Task<MeterResponse> ById(Guid meterId)
    {
        var medidor = await _queries.MeterByIdAsync(meterId);
        await _access.MustOwnOrReachSiteAsync(User.CallerId(), medidor.UserId, medidor.SiteId);
        return MeterResponse.From(medidor);
    }

    /// <summary>
    /// El medidor es del llamante, o esta en un local que alcanza.
    /// </summary>
    /// <remarks>
    /// Cuesta una lectura por clave primaria antes de cada escritura. Es el
    /// precio de dejar la capa de aplicacion sin guardias, que es la que usan
    /// el sembrador y los manejadores de eventos: esos no llaman en nombre de
    /// nadie y no tienen token que comprobar.
    /// </remarks>
    private async Task DebeAlcanzarElMedidorAsync(Guid meterId)
    {
        var medidor = await _queries.MeterByIdAsync(meterId);
        await _access.MustOwnOrReachSiteAsync(User.CallerId(), medidor.UserId, medidor.SiteId);
    }
}

/// <summary>Individual readings sent by the meters.</summary>
[ApiController]
[Route("api/v1/energy-readings")]
[Tags("Energy Readings")]
public sealed class EnergyReadingController : ControllerBase
{
    private readonly EnergyCommandService _commands;
    private readonly EnergyQueryService _queries;

    public EnergyReadingController(EnergyCommandService commands, EnergyQueryService queries)
    {
        _commands = commands;
        _queries = queries;
    }

    /// <summary>Records a new reading.</summary>
    [HttpPost]
    public async Task<ActionResult<ReadingResponse>> Create([FromBody] CreateReadingRequest request)
    {
        // El medidor fisico publica con el token de su usuario, asi que una
        // lectura a nombre de otro solo puede venir de un cliente que se la
        // inventa.
        User.MustBeSelf(request.UserId);
        var reading = await _commands.RecordReadingAsync(request.UserId, request.MeterId,
            request.DeviceId, request.PowerWatts, request.Voltage, request.Current,
            request.Frequency, request.EnergyKwh, request.Timestamp, request.ReadingType,
            request.Phase);
        return StatusCode(StatusCodes.Status201Created, ReadingResponse.From(reading));
    }

    /// <summary>A user's readings, newest first.</summary>
    [HttpGet("user/{userId}")]
    public async Task<List<ReadingResponse>> ByUser(string userId,
        [FromQuery] int limit = 100)
    {
        User.MustBeSelf(userId);
        return Lecturas.Valorizadas(
            await _queries.WithCostAsync(await _queries.ReadingsByUserAsync(userId, limit)));
    }

    /// <summary>Readings from a device.</summary>
    [HttpGet("device/{deviceId}")]
    public async Task<List<ReadingResponse>> ByDevice(string deviceId,
        [FromQuery] int limit = 50, [FromQuery] int skip = 0)
    {
        var lecturas = await _queries.ReadingsByDeviceAsync(deviceId, limit, skip);
        Lecturas.DebenSerDe(User, lecturas);
        return Lecturas.Valorizadas(await _queries.WithCostAsync(lecturas));
    }

    /// <summary>A user's readings within a date range.</summary>
    /// <remarks>
    /// Aqui el usuario llega en la cadena de consulta y no en la ruta, que es
    /// justo lo que lo hacia facil de pasar por alto.
    /// </remarks>
    [HttpGet("range")]
    public async Task<List<ReadingResponse>> ByRange([FromQuery] string userId,
        [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        User.MustBeSelf(userId);
        return Lecturas.Valorizadas(await _queries.WithCostAsync(
            await _queries.ReadingsByRangeAsync(userId, from, to)));
    }

    /// <summary>Latest reading from a meter.</summary>
    [HttpGet("meter/{meterId}/latest")]
    public async Task<ReadingResponse> LatestByMeter(string meterId)
    {
        var lectura = await _queries.LatestByMeterAsync(meterId);
        Lecturas.DebenSerDe(User, new[] { lectura });
        return await Lecturas.Una(_queries, lectura);
    }

    /// <summary>Gets a reading by its identifier.</summary>
    [HttpGet("{readingId:guid}")]
    public async Task<ReadingResponse> ById(Guid readingId)
    {
        var lectura = await _queries.ReadingByIdAsync(readingId);
        Lecturas.DebenSerDe(User, new[] { lectura });
        return await Lecturas.Una(_queries, lectura);
    }

}

/// <summary>
/// Tarifa vigente y consumo por dispositivo.
///
/// <para>La ruta <c>/api/v1/energy/pricing/current</c> es la que consulta el
/// frontend para convertir kWh en soles.</para>
/// </summary>
[ApiController]
[Route("api/v1/energy")]
[Tags("Energy Pricing")]
public sealed class EnergyPricingController : ControllerBase
{
    private readonly EnergyCommandService _commands;
    private readonly EnergyQueryService _queries;

    public EnergyPricingController(EnergyCommandService commands, EnergyQueryService queries)
    {
        _commands = commands;
        _queries = queries;
    }

    /// <summary>Current electricity tariff.</summary>
    [HttpGet("pricing/current")]
    public PricingResponse CurrentPricing() => PricingResponse.From(_commands.CurrentPrice());

    /// <summary>Current commercial tariff for a category of the schedule.</summary>
    [HttpGet("tariffs/{tariffCategory}")]
    public TariffResponse CurrentTariff(string tariffCategory) =>
        TariffResponse.From(_commands.CurrentTariff(tariffCategory));

    /// <summary>Estimates a site's monthly bill.</summary>
    [HttpPost("bill-estimate")]
    public BillEstimateResponse EstimateBill([FromBody] EstimateBillRequest request) =>
        BillEstimateResponse.From(_commands.EstimateBill(request.TariffCategory,
            request.KwhPeak, request.KwhOffPeak, request.MaxDemandKw,
            request.ContractedPowerKw));

    /// <summary>Current consumption of a device.</summary>
    [HttpGet("devices/{deviceId}/consumption/current")]
    public async Task<ReadingResponse> CurrentConsumption(string deviceId)
    {
        var lectura = await _queries.LatestByDeviceAsync(deviceId);
        Lecturas.DebenSerDe(User, new[] { lectura });
        return await Lecturas.Una(_queries, lectura);
    }

    /// <summary>Consumption history of a device.</summary>
    [HttpGet("devices/{deviceId}/consumption/history")]
    public async Task<List<ReadingResponse>> ConsumptionHistory(string deviceId,
        [FromQuery] int limit = 50, [FromQuery] int skip = 0)
    {
        var lecturas = await _queries.ReadingsByDeviceAsync(deviceId, limit, skip);
        Lecturas.DebenSerDe(User, lecturas);
        return Lecturas.Valorizadas(await _queries.WithCostAsync(lecturas));
    }
}

/// <summary>Consumption summaries aggregated by device and period.</summary>
[ApiController]
[Route("api/v1/device-consumptions")]
[Tags("Device Consumptions")]
public sealed class DeviceConsumptionController : ControllerBase
{
    private readonly EnergyQueryService _queries;

    public DeviceConsumptionController(EnergyQueryService queries) => _queries = queries;

    /// <summary>A user's consumption summaries.</summary>
    [HttpGet("user/{userId}")]
    public async Task<List<ConsumptionResponse>> ByUser(string userId)
    {
        User.MustBeSelf(userId);
        return (await _queries.ConsumptionsByUserAsync(userId))
            .Select(ConsumptionResponse.From).ToList();
    }

    /// <summary>A user's highest-consuming devices.</summary>
    [HttpGet("user/{userId}/top")]
    public async Task<List<ConsumptionResponse>> TopByUser(string userId,
        [FromQuery] int limit = 10)
    {
        User.MustBeSelf(userId);
        return (await _queries.TopConsumersByUserAsync(userId, limit))
            .Select(ConsumptionResponse.From).ToList();
    }

    /// <summary>Gets a summary by its identifier.</summary>
    [HttpGet("{consumptionId:guid}")]
    public async Task<ConsumptionResponse> ById(Guid consumptionId)
    {
        var resumen = await _queries.ConsumptionByIdAsync(consumptionId);
        AccessGuards.MustOwn(User.CallerId(), resumen.UserId);
        return ConsumptionResponse.From(resumen);
    }
}

/// <summary>Alerts raised by the monitoring module itself.</summary>
[ApiController]
[Route("api/v1/consumption-alerts")]
[Tags("Consumption Alerts")]
public sealed class ConsumptionAlertController : ControllerBase
{
    private readonly EnergyCommandService _commands;
    private readonly EnergyQueryService _queries;

    public ConsumptionAlertController(EnergyCommandService commands, EnergyQueryService queries)
    {
        _commands = commands;
        _queries = queries;
    }

    /// <summary>Alerts for a user.</summary>
    [HttpGet("user/{userId}")]
    public async Task<List<AlertResponse>> ByUser(string userId)
    {
        User.MustBeSelf(userId);
        return (await _queries.AlertsByUserAsync(userId)).Select(AlertResponse.From).ToList();
    }

    /// <summary>Unread alerts for a user.</summary>
    [HttpGet("user/{userId}/unread")]
    public async Task<List<AlertResponse>> UnreadByUser(string userId)
    {
        User.MustBeSelf(userId);
        return (await _queries.UnreadAlertsByUserAsync(userId))
            .Select(AlertResponse.From).ToList();
    }

    /// <summary>Gets an alert by its identifier.</summary>
    [HttpGet("{alertId:guid}")]
    public async Task<AlertResponse> ById(Guid alertId)
    {
        var aviso = await _queries.AlertByIdAsync(alertId);
        AccessGuards.MustOwn(User.CallerId(), aviso.UserId);
        return AlertResponse.From(aviso);
    }

    /// <summary>Marks an alert as read.</summary>
    [HttpPatch("{alertId:guid}/read")]
    public async Task<AlertResponse> MarkRead(Guid alertId)
    {
        await DebeSerSuyoAsync(alertId);
        return AlertResponse.From(await _commands.MarkAlertReadAsync(alertId));
    }

    /// <summary>Marks an alert as resolved.</summary>
    [HttpPatch("{alertId:guid}/resolve")]
    public async Task<AlertResponse> Resolve(Guid alertId)
    {
        await DebeSerSuyoAsync(alertId);
        return AlertResponse.From(await _commands.ResolveAlertAsync(alertId));
    }

    /// <summary>
    /// Estos avisos los levanta el propio modulo para una persona, no para un
    /// local: no hay membresia a la que recurrir.
    /// </summary>
    private async Task DebeSerSuyoAsync(Guid alertId) =>
        AccessGuards.MustOwn(User.CallerId(),
            (await _queries.AlertByIdAsync(alertId)).UserId);
}

/// <summary>User energy goals.</summary>
[ApiController]
[Route("api/v1/users/{userId}/goals")]
[Tags("User Goals")]
public sealed class UserGoalController : ControllerBase
{
    private readonly EnergyCommandService _commands;
    private readonly EnergyQueryService _queries;

    public UserGoalController(EnergyCommandService commands, EnergyQueryService queries)
    {
        _commands = commands;
        _queries = queries;
    }

    [HttpGet]
    public async Task<GoalResponse> GetGoal(string userId)
    {
        User.MustBeSelf(userId);
        return GoalResponse.From(await _queries.GoalByUserAsync(userId));
    }

    [HttpPut]
    public async Task<GoalResponse> SetGoal(string userId, [FromBody] SetGoalRequest request)
    {
        User.MustBeSelf(userId);
        return GoalResponse.From(await _commands.SetUserGoalAsync(userId, request.MonthlyGoalKwh));
    }
}

/// <summary>
/// Paso de lecturas valorizadas al recurso JSON.
/// </summary>
/// <remarks>
/// Lo comparten los dos controladores que devuelven lecturas, para que el
/// campo del coste aparezca siempre por el mismo camino y no haya endpoints
/// que lo publiquen y otros que no.
/// </remarks>
internal static class Lecturas
{
    /// <summary>
    /// Comprueba contra las propias lecturas de quien son.
    /// </summary>
    /// <remarks>
    /// Una lectura no guarda el local, pero si al usuario. Para las rutas que
    /// filtran por equipo o por medidor no hace falta averiguar de quien es el
    /// aparato: basta mirar lo que se va a devolver. Si no hay nada, no hay
    /// nada que tapar y la respuesta vacia es la correcta.
    /// </remarks>
    public static void DebenSerDe(ClaimsPrincipal llamante,
        IEnumerable<EnergyReading> lecturas)
    {
        var usuario = llamante.CallerId();
        foreach (var lectura in lecturas)
        {
            AccessGuards.MustOwn(usuario, lectura.UserId);
        }
    }

    public static List<ReadingResponse> Valorizadas(
        IEnumerable<EnergyQueryService.ReadingWithCost> lecturas) =>
        lecturas.Select(l => ReadingResponse.From(l.Reading, l.EstimatedCost)).ToList();

    public static async Task<ReadingResponse> Una(EnergyQueryService queries, EnergyReading lectura)
    {
        var valorizada = (await queries.WithCostAsync(new[] { lectura }))[0];
        return ReadingResponse.From(valorizada.Reading, valorizada.EstimatedCost);
    }
}
