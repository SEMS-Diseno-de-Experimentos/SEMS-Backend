using Microsoft.AspNetCore.Mvc;
using Sems.Api.Modules.Alerts.Application;
using Sems.Api.Shared.Http;
using Sems.Api.Shared.Security;
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
    private readonly IAccessPolicy _access;

    public AlertController(AlertCommandService commands, AlertQueryService queries,
        IAccessPolicy access)
    {
        _commands = commands;
        _queries = queries;
        _access = access;
    }

    // ------------------------------------------------------------------ alertas

    /// <summary>Creates an alert.</summary>
    [HttpPost("alerts")]
    public async Task<ActionResult<AlertResponse>> Create([FromBody] CreateAlertRequest r)
    {
        var alert = await _commands.CreateAlertAsync(User.MustBeSelf(r.UserId),
            Guid.Parse(r.DeviceId), OptionalGuid(r.ThresholdId), OptionalGuid(r.InactivityRuleId),
            r.AlertType, r.Title, r.Message, r.Severity, r.Status, r.TriggeredAt);
        return StatusCode(StatusCodes.Status201Created, AlertResponse.From(alert));
    }

    /// <summary>Lists the caller's alerts.</summary>
    /// <remarks>
    /// Antes devolvia las alertas de todos los usuarios, sin ningun filtro. No
    /// hacia falta adivinar un identificador para leer lo de un tercero:
    /// bastaba pedir la lista. Ahora la ruta significa "las mias", que es lo
    /// unico que un endpoint sin identificador puede querer decir.
    /// </remarks>
    [HttpGet("alerts")]
    public async Task<List<AlertResponse>> Mine() =>
        (await _queries.AlertsByUserAsync(User.CallerId())).Select(AlertResponse.From).ToList();

    /// <summary>Gets an alert by its identifier.</summary>
    /// <remarks>
    /// Las de demanda llevan el local al que se refieren, y esas si las tiene
    /// que poder ver quien supervisa ese local aunque el aviso se generara a
    /// nombre de otra persona.
    /// </remarks>
    [HttpGet("alerts/{id:guid}")]
    public async Task<AlertResponse> ById(Guid id)
    {
        var aviso = await _queries.AlertByIdAsync(id);
        await _access.MustOwnOrReachSiteAsync(User.CallerId(), aviso.UserId,
            aviso.SiteId ?? Guid.Empty);
        return AlertResponse.From(aviso);
    }

    /// <summary>Changes the status of an alert.</summary>
    [HttpPatch("alerts/{id:guid}/status")]
    public async Task<AlertResponse> UpdateStatus(Guid id,
        [FromBody] UpdateAlertStatusRequest r)
    {
        var aviso = await _queries.AlertByIdAsync(id);
        await _access.MustOwnOrReachSiteAsync(User.CallerId(), aviso.UserId,
            aviso.SiteId ?? Guid.Empty);
        return AlertResponse.From(await _commands.UpdateStatusAsync(id, r.Status, r.ResolvedAt));
    }

    /// <summary>Alerts for a user.</summary>
    [HttpGet("users/{userId:guid}/alerts")]
    public async Task<List<AlertResponse>> ByUser(Guid userId) =>
        (await _queries.AlertsByUserAsync(User.MustBeSelf(userId)))
        .Select(AlertResponse.From).ToList();

    // ----------------------------------------------------------------- umbrales

    /// <summary>Creates a consumption threshold.</summary>
    [HttpPost("thresholds")]
    public async Task<ActionResult<ThresholdResponse>> CreateThreshold(
        [FromBody] CreateThresholdRequest r)
    {
        var threshold = await _commands.CreateThresholdAsync(User.MustBeSelf(r.UserId),
            Guid.Parse(r.DeviceId), r.ThresholdName, r.Metric, r.Operator, r.ThresholdValue,
            r.Active);
        return StatusCode(StatusCodes.Status201Created, ThresholdResponse.From(threshold));
    }

    /// <summary>A user's thresholds.</summary>
    [HttpGet("users/{userId:guid}/thresholds")]
    public async Task<List<ThresholdResponse>> ThresholdsByUser(Guid userId) =>
        (await _queries.ThresholdsByUserAsync(User.MustBeSelf(userId)))
        .Select(ThresholdResponse.From).ToList();

    // ------------------------------------------------- reglas de inactividad

    /// <summary>Creates an inactivity rule.</summary>
    [HttpPost("inactivity-rules")]
    public async Task<ActionResult<InactivityRuleResponse>> CreateRule(
        [FromBody] CreateInactivityRuleRequest r)
    {
        var rule = await _commands.CreateInactivityRuleAsync(User.MustBeSelf(r.UserId),
            Guid.Parse(r.DeviceId), r.RuleName, r.MaxInactiveMinutes, r.Active);
        return StatusCode(StatusCodes.Status201Created, InactivityRuleResponse.From(rule));
    }

    /// <summary>A user's inactivity rules.</summary>
    [HttpGet("users/{userId:guid}/inactivity-rules")]
    public async Task<List<InactivityRuleResponse>> RulesByUser(Guid userId) =>
        (await _queries.RulesByUserAsync(User.MustBeSelf(userId)))
        .Select(InactivityRuleResponse.From).ToList();

    // ------------------------------------------ preferencias de notificacion

    /// <summary>Saves a notification preference.</summary>
    [HttpPost("notification-preferences")]
    public async Task<ActionResult<PreferenceResponse>> CreatePreference(
        [FromBody] CreatePreferenceRequest r)
    {
        var preference = await _commands.CreatePreferenceAsync(User.MustBeSelf(r.UserId), r.Channel,
            r.Enabled, r.MinSeverity, r.QuietHoursStart, r.QuietHoursEnd);
        return StatusCode(StatusCodes.Status201Created, PreferenceResponse.From(preference));
    }

    /// <summary>A user's notification preferences.</summary>
    [HttpGet("users/{userId:guid}/notification-preferences")]
    public async Task<List<PreferenceResponse>> PreferencesByUser(Guid userId) =>
        (await _queries.PreferencesByUserAsync(User.MustBeSelf(userId)))
        .Select(PreferenceResponse.From).ToList();

    // ------------------------------------------------------ reglas de demanda

    /// <summary>Creates a demand-watch rule for a site.</summary>
    [HttpPost("demand-rules")]
    public async Task<ActionResult<DemandRuleResponse>> CreateDemandRule(
        [FromBody] CreateDemandRuleRequest request)
    {
        var regla = await _commands.CreateDemandRuleAsync(Guid.Parse(request.SiteId),
            User.MustBeSelf(request.UserId), request.RuleName, request.ContractedPowerKw,
            request.WarningPercent, request.Active);
        return StatusCode(StatusCodes.Status201Created, DemandRuleResponse.From(regla));
    }

    /// <summary>Active demand rules for a site.</summary>
    [HttpGet("sites/{siteId:guid}/demand-rules")]
    public async Task<List<DemandRuleResponse>> DemandRulesBySite(Guid siteId)
    {
        await _access.MustReachSiteAsync(User.CallerId(), siteId);
        return (await _commands.DemandRulesBySiteAsync(siteId))
            .Select(DemandRuleResponse.From).ToList();
    }

    /// <summary>
    /// Evaluates a measured demand against the site rules and raises the
    /// corresponding alerts.
    /// </summary>
    [HttpPost("sites/{siteId:guid}/demand-evaluations")]
    public async Task<List<AlertResponse>> EvaluateDemand(Guid siteId,
        [FromBody] EvaluateDemandRequest request)
    {
        // Levanta avisos en el local, asi que quien no lo alcanza tampoco
        // puede llenarlo de alarmas inventadas.
        await _access.MustReachSiteAsync(User.CallerId(), siteId);
        return (await _commands.EvaluateDemandAsync(siteId, request.DemandKw))
            .Select(AlertResponse.From).ToList();
    }

    private static Guid? OptionalGuid(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Guid.Parse(value);
}
