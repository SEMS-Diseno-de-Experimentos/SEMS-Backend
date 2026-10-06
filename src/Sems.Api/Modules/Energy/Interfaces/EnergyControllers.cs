using Microsoft.AspNetCore.Mvc;
using Sems.Api.Modules.Energy.Application;
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

    public EnergyMeterController(EnergyCommandService commands, EnergyQueryService queries)
    {
        _commands = commands;
        _queries = queries;
    }

    /// <summary>Registers a new meter.</summary>
    [HttpPost]
    public async Task<ActionResult<MeterResponse>> Register([FromBody] RegisterMeterRequest request)
    {
        var meter = await _commands.RegisterMeterAsync(request.UserId, request.MeterSerial,
            request.Model, request.Brand, request.Location, request.FirmwareVersion,
            request.MaxPowerWatts);
        return StatusCode(StatusCodes.Status201Created, MeterResponse.From(meter));
    }

    /// <summary>Lists a user's meters.</summary>
    [HttpGet("user/{userId}")]
    public async Task<List<MeterResponse>> ByUser(string userId) =>
        (await _queries.MetersByUserAsync(userId)).Select(MeterResponse.From).ToList();

    /// <summary>Deactivates a meter.</summary>
    [HttpPatch("{meterId:guid}/deactivate")]
    public async Task<MeterResponse> Deactivate(Guid meterId) =>
        MeterResponse.From(await _commands.DeactivateMeterAsync(meterId));

    /// <summary>Gets a meter by its identifier.</summary>
    [HttpGet("{meterId:guid}")]
    public async Task<MeterResponse> ById(Guid meterId) =>
        MeterResponse.From(await _queries.MeterByIdAsync(meterId));
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
        var reading = await _commands.RecordReadingAsync(request.UserId, request.MeterId,
            request.DeviceId, request.PowerWatts, request.Voltage, request.Current,
            request.Frequency, request.EnergyKwh, request.Timestamp, request.ReadingType,
            request.Phase);
        return StatusCode(StatusCodes.Status201Created, ReadingResponse.From(reading));
    }

    /// <summary>A user's readings, newest first.</summary>
    [HttpGet("user/{userId}")]
    public async Task<List<ReadingResponse>> ByUser(string userId,
        [FromQuery] int limit = 100) =>
        (await _queries.ReadingsByUserAsync(userId, limit)).Select(ReadingResponse.From).ToList();

    /// <summary>Readings from a device.</summary>
    [HttpGet("device/{deviceId}")]
    public async Task<List<ReadingResponse>> ByDevice(string deviceId,
        [FromQuery] int limit = 50, [FromQuery] int skip = 0) =>
        (await _queries.ReadingsByDeviceAsync(deviceId, limit, skip))
        .Select(ReadingResponse.From).ToList();

    /// <summary>A user's readings within a date range.</summary>
    [HttpGet("range")]
    public async Task<List<ReadingResponse>> ByRange([FromQuery] string userId,
        [FromQuery] DateTime from, [FromQuery] DateTime to) =>
        (await _queries.ReadingsByRangeAsync(userId, from, to)).Select(ReadingResponse.From).ToList();

    /// <summary>Latest reading from a meter.</summary>
    [HttpGet("meter/{meterId}/latest")]
    public async Task<ReadingResponse> LatestByMeter(string meterId) =>
        ReadingResponse.From(await _queries.LatestByMeterAsync(meterId));

    /// <summary>Gets a reading by its identifier.</summary>
    [HttpGet("{readingId:guid}")]
    public async Task<ReadingResponse> ById(Guid readingId) =>
        ReadingResponse.From(await _queries.ReadingByIdAsync(readingId));
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
    public async Task<ReadingResponse> CurrentConsumption(string deviceId) =>
        ReadingResponse.From(await _queries.LatestByDeviceAsync(deviceId));

    /// <summary>Consumption history of a device.</summary>
    [HttpGet("devices/{deviceId}/consumption/history")]
    public async Task<List<ReadingResponse>> ConsumptionHistory(string deviceId,
        [FromQuery] int limit = 50, [FromQuery] int skip = 0) =>
        (await _queries.ReadingsByDeviceAsync(deviceId, limit, skip))
        .Select(ReadingResponse.From).ToList();
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
    public async Task<List<ConsumptionResponse>> ByUser(string userId) =>
        (await _queries.ConsumptionsByUserAsync(userId)).Select(ConsumptionResponse.From).ToList();

    /// <summary>A user's highest-consuming devices.</summary>
    [HttpGet("user/{userId}/top")]
    public async Task<List<ConsumptionResponse>> TopByUser(string userId,
        [FromQuery] int limit = 10) =>
        (await _queries.TopConsumersByUserAsync(userId, limit))
        .Select(ConsumptionResponse.From).ToList();

    /// <summary>Gets a summary by its identifier.</summary>
    [HttpGet("{consumptionId:guid}")]
    public async Task<ConsumptionResponse> ById(Guid consumptionId) =>
        ConsumptionResponse.From(await _queries.ConsumptionByIdAsync(consumptionId));
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
    public async Task<List<AlertResponse>> ByUser(string userId) =>
        (await _queries.AlertsByUserAsync(userId)).Select(AlertResponse.From).ToList();

    /// <summary>Unread alerts for a user.</summary>
    [HttpGet("user/{userId}/unread")]
    public async Task<List<AlertResponse>> UnreadByUser(string userId) =>
        (await _queries.UnreadAlertsByUserAsync(userId)).Select(AlertResponse.From).ToList();

    /// <summary>Gets an alert by its identifier.</summary>
    [HttpGet("{alertId:guid}")]
    public async Task<AlertResponse> ById(Guid alertId) =>
        AlertResponse.From(await _queries.AlertByIdAsync(alertId));

    /// <summary>Marks an alert as read.</summary>
    [HttpPatch("{alertId:guid}/read")]
    public async Task<AlertResponse> MarkRead(Guid alertId) =>
        AlertResponse.From(await _commands.MarkAlertReadAsync(alertId));

    /// <summary>Marks an alert as resolved.</summary>
    [HttpPatch("{alertId:guid}/resolve")]
    public async Task<AlertResponse> Resolve(Guid alertId) =>
        AlertResponse.From(await _commands.ResolveAlertAsync(alertId));
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
    public async Task<GoalResponse> GetGoal(string userId) =>
        GoalResponse.From(await _queries.GoalByUserAsync(userId));

    [HttpPut]
    public async Task<GoalResponse> SetGoal(string userId, [FromBody] SetGoalRequest request) =>
        GoalResponse.From(await _commands.SetUserGoalAsync(userId, request.MonthlyGoalKwh));
}
