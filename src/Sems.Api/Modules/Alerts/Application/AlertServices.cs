using Sems.Api.Modules.Alerts.Domain.Model;
using Sems.Api.Modules.Alerts.Domain.Repositories;
using Sems.Api.Shared.Errors;
using Sems.Api.Shared.Events;

namespace Sems.Api.Modules.Alerts.Application;

/// <summary>Casos de uso que modifican el estado del modulo de alertas.</summary>
public sealed class AlertCommandService
{
    private readonly IAlertRepository _alerts;
    private readonly IThresholdRepository _thresholds;
    private readonly IInactivityRuleRepository _rules;
    private readonly IDemandRuleRepository _demandRules;
    private readonly INotificationPreferenceRepository _preferences;
    private readonly IDomainEventBus _bus;

    public AlertCommandService(IAlertRepository alerts, IThresholdRepository thresholds,
        IInactivityRuleRepository rules, IDemandRuleRepository demandRules,
        INotificationPreferenceRepository preferences, IDomainEventBus bus)
    {
        _alerts = alerts;
        _thresholds = thresholds;
        _rules = rules;
        _demandRules = demandRules;
        _preferences = preferences;
        _bus = bus;
    }

    // ------------------------------------------------------ reglas de demanda

    public async Task<DemandRule> CreateDemandRuleAsync(Guid siteId, Guid userId, string? ruleName,
        double contractedPowerKw, double? warningPercent, bool? active,
        CancellationToken ct = default)
    {
        var regla = DemandRule.Create(siteId, userId, ruleName, contractedPowerKw, warningPercent,
            active);
        return await _demandRules.SaveAsync(regla, ct);
    }

    public Task<List<DemandRule>> DemandRulesBySiteAsync(Guid siteId, CancellationToken ct = default) =>
        _demandRules.FindActiveBySiteIdAsync(siteId, ct);

    /// <summary>
    /// Evalua una demanda medida contra las reglas del local y levanta la alerta
    /// que corresponda.
    /// </summary>
    /// <remarks>
    /// <para>Se levanta una alerta por regla incumplida, no una por local: un
    /// local puede tener una regla estricta para el turno de noche y otra mas
    /// laxa para el dia.</para>
    ///
    /// <para>La severidad distingue los dos casos porque la accion del cliente
    /// es distinta: con WARNING todavia puede apagar algo y evitar el recargo;
    /// con EXCEEDED el cargo del mes ya esta hecho y lo unico util es que no se
    /// repita.</para>
    /// </remarks>
    public async Task<List<Alert>> EvaluateDemandAsync(Guid siteId, double demandaKw,
        CancellationToken ct = default)
    {
        var levantadas = new List<Alert>();

        foreach (var regla in await _demandRules.FindActiveBySiteIdAsync(siteId, ct))
        {
            var nivel = regla.Evaluar(demandaKw);
            if (nivel == DemandLevel.OK)
            {
                continue;
            }

            var margen = regla.MargenKw(demandaKw);
            var (titulo, mensaje, severidad) = nivel == DemandLevel.WARNING
                ? ("Demanda cerca de lo contratado",
                   $"El local esta en {demandaKw:0.##} kW de {regla.ContractedPowerKw:0.##} kW contratados. "
                   + $"Quedan {margen:0.##} kW de margen.",
                   "WARNING")
                : ("Demanda por encima de lo contratado",
                   $"El local alcanzo {demandaKw:0.##} kW, {Math.Abs(margen):0.##} kW por encima de los "
                   + $"{regla.ContractedPowerKw:0.##} kW contratados. El recargo por exceso se aplica a "
                   + "todo el mes.",
                   "CRITICAL");

            var alerta = Alert.Raise(regla.UserId, null, null, null, "DEMAND", titulo, mensaje,
                severidad, null, DateTime.UtcNow);
            levantadas.Add(await _alerts.SaveAsync(alerta, ct));
        }

        return levantadas;
    }

    public async Task<Alert> CreateAlertAsync(Guid userId, Guid? deviceId, Guid? thresholdId,
        Guid? inactivityRuleId, string? alertType, string? title, string? message, string? severity,
        string? status, DateTime? triggeredAt, CancellationToken ct = default)
    {
        var alert = Alert.Raise(userId, deviceId, thresholdId, inactivityRuleId, alertType, title,
            message, severity, status, triggeredAt);

        // El envio del correo lo dispara este evento, no una llamada directa:
        // asi la alerta se guarda aunque el servidor de correo este caido.
        _bus.Publish(new DomainEvents.AlertTriggered(userId, alert.AlertId, alertType ?? string.Empty,
            severity ?? string.Empty, message ?? string.Empty));

        return await _alerts.SaveAsync(alert, ct);
    }

    public async Task<Alert> UpdateStatusAsync(Guid alertId, string status, DateTime? resolvedAt,
        CancellationToken ct = default)
    {
        var alert = await _alerts.FindByIdAsync(alertId, ct)
                    ?? throw AppException.NotFound("alert not found");
        alert.UpdateStatus(status, resolvedAt);
        return await _alerts.SaveAsync(alert, ct);
    }

    public Task<AlertThreshold> CreateThresholdAsync(Guid userId, Guid? deviceId, string? name,
        string? metric, string? op, double value, bool? active, CancellationToken ct = default) =>
        _thresholds.SaveAsync(AlertThreshold.Create(userId, deviceId, name, metric,
            OperatorExtensions.ToOperator(op), value, active), ct);

    public Task<InactivityRule> CreateInactivityRuleAsync(Guid userId, Guid? deviceId,
        string? ruleName, int maxInactiveMinutes, bool active, CancellationToken ct = default)
    {
        if (maxInactiveMinutes <= 0)
        {
            throw AppException.Validation("max_inactive_minutes must be greater than zero");
        }
        return _rules.SaveAsync(InactivityRule.Create(userId, deviceId, ruleName,
            maxInactiveMinutes, active), ct);
    }

    /// <summary>Repetir el canal actualiza la preferencia en vez de duplicarla.</summary>
    public async Task<NotificationPreference> CreatePreferenceAsync(Guid userId, string channel,
        bool enabled, string? minSeverity, DateTime? quietStart, DateTime? quietEnd,
        CancellationToken ct = default)
    {
        var existing = await _preferences.FindByUserIdAndChannelAsync(userId, channel, ct);
        if (existing is not null)
        {
            existing.Update(enabled, minSeverity, quietStart, quietEnd);
            return await _preferences.SaveAsync(existing, ct);
        }

        return await _preferences.SaveAsync(NotificationPreference.Create(userId, channel, enabled,
            minSeverity, quietStart, quietEnd), ct);
    }
}

/// <summary>Consultas del modulo de alertas.</summary>
public sealed class AlertQueryService
{
    private readonly IAlertRepository _alerts;
    private readonly IThresholdRepository _thresholds;
    private readonly IInactivityRuleRepository _rules;
    private readonly INotificationPreferenceRepository _preferences;

    public AlertQueryService(IAlertRepository alerts, IThresholdRepository thresholds,
        IInactivityRuleRepository rules, INotificationPreferenceRepository preferences)
    {
        _alerts = alerts;
        _thresholds = thresholds;
        _rules = rules;
        _preferences = preferences;
    }

    public Task<List<Alert>> AllAlertsAsync(CancellationToken ct = default) =>
        _alerts.FindAllAsync(ct);

    public async Task<Alert> AlertByIdAsync(Guid alertId, CancellationToken ct = default) =>
        await _alerts.FindByIdAsync(alertId, ct) ?? throw AppException.NotFound("alert not found");

    public Task<List<Alert>> AlertsByUserAsync(Guid userId, CancellationToken ct = default) =>
        _alerts.FindByUserIdAsync(userId, ct);

    public Task<List<AlertThreshold>> ThresholdsByUserAsync(Guid userId, CancellationToken ct = default) =>
        _thresholds.FindByUserIdAsync(userId, ct);

    public Task<List<InactivityRule>> RulesByUserAsync(Guid userId, CancellationToken ct = default) =>
        _rules.FindByUserIdAsync(userId, ct);

    public Task<List<NotificationPreference>> PreferencesByUserAsync(Guid userId,
        CancellationToken ct = default) => _preferences.FindByUserIdAsync(userId, ct);
}
