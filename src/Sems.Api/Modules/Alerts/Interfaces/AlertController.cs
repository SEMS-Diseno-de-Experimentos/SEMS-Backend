using Microsoft.AspNetCore.Mvc;
using Sems.Api.Modules.Alerts.Application;
using static Sems.Api.Modules.Alerts.Interfaces.AlertResources;

namespace Sems.Api.Modules.Alerts.Interfaces;

/// <summary>
/// API REST del bounded context de alertas.
///
/// <para>Rutas identicas a las del microservicio en Go bajo <c>/api/v1</c>.</para>
/// </summary>
[ApiController]
[Route("api/v1")]
[Tags("Alerts")]
public sealed class AlertController : ControllerBase
{
    private readonly AlertCommandService _commands;
    private readonly AlertQueryService _queries;

    public AlertController(AlertCommandService commands, AlertQueryService queries)
    {
        _commands = commands;
        _queries = queries;
    }

    // ------------------------------------------------------------------ alertas

    /// <summary>Crea una alerta.</summary>
    [HttpPost("alerts")]
    public async Task<ActionResult<AlertResponse>> Create([FromBody] CreateAlertRequest r)
    {
        var alert = await _commands.CreateAlertAsync(Guid.Parse(r.UserId),
            Guid.Parse(r.DeviceId), OptionalGuid(r.ThresholdId), OptionalGuid(r.InactivityRuleId),
            r.AlertType, r.Title, r.Message, r.Severity, r.Status, r.TriggeredAt);
        return StatusCode(StatusCodes.Status201Created, AlertResponse.From(alert));
    }

    /// <summary>Lista todas las alertas.</summary>
    [HttpGet("alerts")]
    public async Task<List<AlertResponse>> All() =>
        (await _queries.AllAlertsAsync()).Select(AlertResponse.From).ToList();

    /// <summary>Obtiene una alerta por su identificador.</summary>
    [HttpGet("alerts/{id:guid}")]
    public async Task<AlertResponse> ById(Guid id) =>
        AlertResponse.From(await _queries.AlertByIdAsync(id));

    /// <summary>Cambia el estado de una alerta.</summary>
    [HttpPatch("alerts/{id:guid}/status")]
    public async Task<AlertResponse> UpdateStatus(Guid id,
        [FromBody] UpdateAlertStatusRequest r) =>
        AlertResponse.From(await _commands.UpdateStatusAsync(id, r.Status, r.ResolvedAt));

    /// <summary>Alertas de un usuario.</summary>
    [HttpGet("users/{userId:guid}/alerts")]
    public async Task<List<AlertResponse>> ByUser(Guid userId) =>
        (await _queries.AlertsByUserAsync(userId)).Select(AlertResponse.From).ToList();

    // ----------------------------------------------------------------- umbrales

    /// <summary>Crea un umbral de consumo.</summary>
    [HttpPost("thresholds")]
    public async Task<ActionResult<ThresholdResponse>> CreateThreshold(
        [FromBody] CreateThresholdRequest r)
    {
        var threshold = await _commands.CreateThresholdAsync(Guid.Parse(r.UserId),
            Guid.Parse(r.DeviceId), r.ThresholdName, r.Metric, r.Operator, r.ThresholdValue,
            r.Active);
        return StatusCode(StatusCodes.Status201Created, ThresholdResponse.From(threshold));
    }

    /// <summary>Umbrales de un usuario.</summary>
    [HttpGet("users/{userId:guid}/thresholds")]
    public async Task<List<ThresholdResponse>> ThresholdsByUser(Guid userId) =>
        (await _queries.ThresholdsByUserAsync(userId)).Select(ThresholdResponse.From).ToList();

    // ------------------------------------------------- reglas de inactividad

    /// <summary>Crea una regla de inactividad.</summary>
    [HttpPost("inactivity-rules")]
    public async Task<ActionResult<InactivityRuleResponse>> CreateRule(
        [FromBody] CreateInactivityRuleRequest r)
    {
        var rule = await _commands.CreateInactivityRuleAsync(Guid.Parse(r.UserId),
            Guid.Parse(r.DeviceId), r.RuleName, r.MaxInactiveMinutes, r.Active);
        return StatusCode(StatusCodes.Status201Created, InactivityRuleResponse.From(rule));
    }

    /// <summary>Reglas de inactividad de un usuario.</summary>
    [HttpGet("users/{userId:guid}/inactivity-rules")]
    public async Task<List<InactivityRuleResponse>> RulesByUser(Guid userId) =>
        (await _queries.RulesByUserAsync(userId)).Select(InactivityRuleResponse.From).ToList();

    // ------------------------------------------ preferencias de notificacion

    /// <summary>Guarda una preferencia de notificacion.</summary>
    [HttpPost("notification-preferences")]
    public async Task<ActionResult<PreferenceResponse>> CreatePreference(
        [FromBody] CreatePreferenceRequest r)
    {
        var preference = await _commands.CreatePreferenceAsync(Guid.Parse(r.UserId), r.Channel,
            r.Enabled, r.MinSeverity, r.QuietHoursStart, r.QuietHoursEnd);
        return StatusCode(StatusCodes.Status201Created, PreferenceResponse.From(preference));
    }

    /// <summary>Preferencias de notificacion de un usuario.</summary>
    [HttpGet("users/{userId:guid}/notification-preferences")]
    public async Task<List<PreferenceResponse>> PreferencesByUser(Guid userId) =>
        (await _queries.PreferencesByUserAsync(userId)).Select(PreferenceResponse.From).ToList();

    // ------------------------------------------------------ reglas de demanda

    /// <summary>Crea una regla de vigilancia de demanda para un local.</summary>
    [HttpPost("demand-rules")]
    public async Task<ActionResult<DemandRuleResponse>> CreateDemandRule(
        [FromBody] CreateDemandRuleRequest request)
    {
        var regla = await _commands.CreateDemandRuleAsync(Guid.Parse(request.SiteId),
            Guid.Parse(request.UserId), request.RuleName, request.ContractedPowerKw,
            request.WarningPercent, request.Active);
        return StatusCode(StatusCodes.Status201Created, DemandRuleResponse.From(regla));
    }

    /// <summary>Reglas de demanda activas de un local.</summary>
    [HttpGet("sites/{siteId:guid}/demand-rules")]
    public async Task<List<DemandRuleResponse>> DemandRulesBySite(Guid siteId) =>
        (await _commands.DemandRulesBySiteAsync(siteId)).Select(DemandRuleResponse.From).ToList();

    /// <summary>
    /// Evalua una demanda medida contra las reglas del local y levanta las
    /// alertas que correspondan.
    /// </summary>
    [HttpPost("sites/{siteId:guid}/demand-evaluations")]
    public async Task<List<AlertResponse>> EvaluateDemand(Guid siteId,
        [FromBody] EvaluateDemandRequest request) =>
        (await _commands.EvaluateDemandAsync(siteId, request.DemandKw))
            .Select(AlertResponse.From).ToList();

    private static Guid? OptionalGuid(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Guid.Parse(value);
}
