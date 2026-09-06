using Microsoft.AspNetCore.Mvc;
using Sems.Api.Modules.Energy.Application;
using static Sems.Api.Modules.Energy.Interfaces.EnergyResources;

namespace Sems.Api.Modules.Energy.Interfaces;

/// <summary>Medidores EOS vinculados a cada usuario.</summary>
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

    /// <summary>Registra un medidor nuevo.</summary>
    [HttpPost]
    public async Task<ActionResult<MeterResponse>> Register([FromBody] RegisterMeterRequest request)
    {
        var meter = await _commands.RegisterMeterAsync(request.UserId, request.MeterSerial,
            request.Model, request.Brand, request.Location, request.FirmwareVersion,
            request.MaxPowerWatts);
        return StatusCode(StatusCodes.Status201Created, MeterResponse.From(meter));
    }

    /// <summary>Lista los medidores de un usuario.</summary>
    [HttpGet("user/{userId}")]
    public async Task<List<MeterResponse>> ByUser(string userId) =>
        (await _queries.MetersByUserAsync(userId)).Select(MeterResponse.From).ToList();

    /// <summary>Desactiva un medidor.</summary>
    [HttpPatch("{meterId:guid}/deactivate")]
    public async Task<MeterResponse> Deactivate(Guid meterId) =>
        MeterResponse.From(await _commands.DeactivateMeterAsync(meterId));

    /// <summary>Obtiene un medidor por su identificador.</summary>
    [HttpGet("{meterId:guid}")]
    public async Task<MeterResponse> ById(Guid meterId) =>
        MeterResponse.From(await _queries.MeterByIdAsync(meterId));
}

/// <summary>Lecturas individuales enviadas por los medidores.</summary>
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

    /// <summary>Registra una lectura nueva.</summary>
    [HttpPost]
    public async Task<ActionResult<ReadingResponse>> Create([FromBody] CreateReadingRequest request)
    {
        var reading = await _commands.RecordReadingAsync(request.UserId, request.MeterId,
            request.DeviceId, request.PowerWatts, request.Voltage, request.Current,
            request.Frequency, request.EnergyKwh, request.Timestamp, request.ReadingType,
            request.Phase);
        return StatusCode(StatusCodes.Status201Created, ReadingResponse.From(reading));
    }

    /// <summary>Lecturas de un usuario, de la mas reciente a la mas antigua.</summary>
    [HttpGet("user/{userId}")]
    public async Task<List<ReadingResponse>> ByUser(string userId,
        [FromQuery] int limit = 100) =>
        (await _queries.ReadingsByUserAsync(userId, limit)).Select(ReadingResponse.From).ToList();

    /// <summary>Lecturas de un dispositivo.</summary>
    [HttpGet("device/{deviceId}")]
    public async Task<List<ReadingResponse>> ByDevice(string deviceId,
        [FromQuery] int limit = 50, [FromQuery] int skip = 0) =>
        (await _queries.ReadingsByDeviceAsync(deviceId, limit, skip))
        .Select(ReadingResponse.From).ToList();

    /// <summary>Lecturas de un usuario dentro de un rango de fechas.</summary>
    [HttpGet("range")]
    public async Task<List<ReadingResponse>> ByRange([FromQuery] string userId,
        [FromQuery] DateTime from, [FromQuery] DateTime to) =>
        (await _queries.ReadingsByRangeAsync(userId, from, to)).Select(ReadingResponse.From).ToList();

    /// <summary>Ultima lectura de un medidor.</summary>
    [HttpGet("meter/{meterId}/latest")]
    public async Task<ReadingResponse> LatestByMeter(string meterId) =>
        ReadingResponse.From(await _queries.LatestByMeterAsync(meterId));

    /// <summary>Obtiene una lectura por su identificador.</summary>
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

    /// <summary>Tarifa electrica vigente.</summary>
    [HttpGet("pricing/current")]
    public PricingResponse CurrentPricing() => PricingResponse.From(_commands.CurrentPrice());

    /// <summary>Tarifa comercial vigente para una categoria del pliego.</summary>
    [HttpGet("tariffs/{tariffCategory}")]
    public TariffResponse CurrentTariff(string tariffCategory) =>
        TariffResponse.From(_commands.CurrentTariff(tariffCategory));

    /// <summary>Estima la factura del mes de un local.</summary>
    [HttpPost("bill-estimate")]
    public BillEstimateResponse EstimateBill([FromBody] EstimateBillRequest request) =>
        BillEstimateResponse.From(_commands.EstimateBill(request.TariffCategory,
            request.KwhPeak, request.KwhOffPeak, request.MaxDemandKw,
            request.ContractedPowerKw));

    /// <summary>Consumo actual de un dispositivo.</summary>
    [HttpGet("devices/{deviceId}/consumption/current")]
    public async Task<ReadingResponse> CurrentConsumption(string deviceId) =>
        ReadingResponse.From(await _queries.LatestByDeviceAsync(deviceId));

    /// <summary>Historial de consumo de un dispositivo.</summary>
    [HttpGet("devices/{deviceId}/consumption/history")]
    public async Task<List<ReadingResponse>> ConsumptionHistory(string deviceId,
        [FromQuery] int limit = 50, [FromQuery] int skip = 0) =>
        (await _queries.ReadingsByDeviceAsync(deviceId, limit, skip))
        .Select(ReadingResponse.From).ToList();
}

/// <summary>Resumenes de consumo agregados por dispositivo y periodo.</summary>
[ApiController]
[Route("api/v1/device-consumptions")]
[Tags("Device Consumptions")]
public sealed class DeviceConsumptionController : ControllerBase
{
    private readonly EnergyQueryService _queries;

    public DeviceConsumptionController(EnergyQueryService queries) => _queries = queries;

    /// <summary>Resumenes de consumo de un usuario.</summary>
    [HttpGet("user/{userId}")]
    public async Task<List<ConsumptionResponse>> ByUser(string userId) =>
        (await _queries.ConsumptionsByUserAsync(userId)).Select(ConsumptionResponse.From).ToList();

    /// <summary>Dispositivos que mas consumen de un usuario.</summary>
    [HttpGet("user/{userId}/top")]
    public async Task<List<ConsumptionResponse>> TopByUser(string userId,
        [FromQuery] int limit = 10) =>
        (await _queries.TopConsumersByUserAsync(userId, limit))
        .Select(ConsumptionResponse.From).ToList();

    /// <summary>Obtiene un resumen por su identificador.</summary>
    [HttpGet("{consumptionId:guid}")]
    public async Task<ConsumptionResponse> ById(Guid consumptionId) =>
        ConsumptionResponse.From(await _queries.ConsumptionByIdAsync(consumptionId));
}

/// <summary>Alertas generadas por el propio modulo de monitoreo.</summary>
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

    /// <summary>Alertas de un usuario.</summary>
    [HttpGet("user/{userId}")]
    public async Task<List<AlertResponse>> ByUser(string userId) =>
        (await _queries.AlertsByUserAsync(userId)).Select(AlertResponse.From).ToList();

    /// <summary>Alertas sin leer de un usuario.</summary>
    [HttpGet("user/{userId}/unread")]
    public async Task<List<AlertResponse>> UnreadByUser(string userId) =>
        (await _queries.UnreadAlertsByUserAsync(userId)).Select(AlertResponse.From).ToList();

    /// <summary>Obtiene una alerta por su identificador.</summary>
    [HttpGet("{alertId:guid}")]
    public async Task<AlertResponse> ById(Guid alertId) =>
        AlertResponse.From(await _queries.AlertByIdAsync(alertId));

    /// <summary>Marca una alerta como leida.</summary>
    [HttpPatch("{alertId:guid}/read")]
    public async Task<AlertResponse> MarkRead(Guid alertId) =>
        AlertResponse.From(await _commands.MarkAlertReadAsync(alertId));

    /// <summary>Da por resuelta una alerta.</summary>
    [HttpPatch("{alertId:guid}/resolve")]
    public async Task<AlertResponse> Resolve(Guid alertId) =>
        AlertResponse.From(await _commands.ResolveAlertAsync(alertId));
}
