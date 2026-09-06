using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Sems.Api.Modules.Alerts.Domain.Model;

namespace Sems.Api.Modules.Alerts.Interfaces;

/// <summary>Contrato JSON del modulo de alertas, en snake_case como el original.</summary>
public static class AlertResources
{
    // ------------------------------------------------------------- peticiones

    public sealed record CreateAlertRequest(
        [property: JsonPropertyName("user_id")]
        [Required(ErrorMessage = "is required")] string UserId,
        [property: JsonPropertyName("device_id")]
        [Required(ErrorMessage = "is required")] string DeviceId,
        [property: JsonPropertyName("threshold_id")] string? ThresholdId,
        [property: JsonPropertyName("inactivity_rule_id")] string? InactivityRuleId,
        [property: JsonPropertyName("alert_type")]
        [Required(ErrorMessage = "is required")] string AlertType,
        [property: JsonPropertyName("title")]
        [Required(ErrorMessage = "is required")] string Title,
        [property: JsonPropertyName("message")]
        [Required(ErrorMessage = "is required")] string Message,
        [property: JsonPropertyName("severity")]
        [Required(ErrorMessage = "is required")] string Severity,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("triggered_at")] DateTime? TriggeredAt);

    public sealed record UpdateAlertStatusRequest(
        [property: JsonPropertyName("status")]
        [Required(ErrorMessage = "is required")] string Status,
        [property: JsonPropertyName("resolved_at")] DateTime? ResolvedAt);

    public sealed record CreateThresholdRequest(
        [property: JsonPropertyName("user_id")]
        [Required(ErrorMessage = "is required")] string UserId,
        [property: JsonPropertyName("device_id")]
        [Required(ErrorMessage = "is required")] string DeviceId,
        [property: JsonPropertyName("threshold_name")]
        [Required(ErrorMessage = "is required")] string ThresholdName,
        [property: JsonPropertyName("metric")]
        [Required(ErrorMessage = "is required")] string Metric,
        [property: JsonPropertyName("operator")]
        [Required(ErrorMessage = "is required")] string Operator,
        [property: JsonPropertyName("threshold_value")] double ThresholdValue,
        [property: JsonPropertyName("active")] bool? Active);

    public sealed record CreateInactivityRuleRequest(
        [property: JsonPropertyName("user_id")]
        [Required(ErrorMessage = "is required")] string UserId,
        [property: JsonPropertyName("device_id")]
        [Required(ErrorMessage = "is required")] string DeviceId,
        [property: JsonPropertyName("rule_name")]
        [Required(ErrorMessage = "is required")] string RuleName,
        [property: JsonPropertyName("max_inactive_minutes")] int MaxInactiveMinutes,
        [property: JsonPropertyName("active")] bool Active);

    public sealed record CreatePreferenceRequest(
        [property: JsonPropertyName("user_id")]
        [Required(ErrorMessage = "is required")] string UserId,
        [property: JsonPropertyName("channel")]
        [Required(ErrorMessage = "is required")] string Channel,
        [property: JsonPropertyName("enabled")] bool Enabled,
        [property: JsonPropertyName("min_severity")] string? MinSeverity,
        [property: JsonPropertyName("quiet_hours_start")] DateTime? QuietHoursStart,
        [property: JsonPropertyName("quiet_hours_end")] DateTime? QuietHoursEnd);

    public sealed record CreateDemandRuleRequest(
        [property: JsonPropertyName("site_id")]
        [Required(ErrorMessage = "is required")] string SiteId,
        [property: JsonPropertyName("user_id")]
        [Required(ErrorMessage = "is required")] string UserId,
        [property: JsonPropertyName("rule_name")] string? RuleName,
        [property: JsonPropertyName("contracted_power_kw")]
        [Required(ErrorMessage = "is required")] double ContractedPowerKw,
        [property: JsonPropertyName("warning_percent")] double? WarningPercent,
        [property: JsonPropertyName("active")] bool? Active);

    public sealed record EvaluateDemandRequest(
        [property: JsonPropertyName("demand_kw")]
        [Required(ErrorMessage = "is required")] double DemandKw);

    public sealed record DemandRuleResponse(
        [property: JsonPropertyName("demand_rule_id")] string DemandRuleId,
        [property: JsonPropertyName("site_id")] string SiteId,
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("rule_name")] string? RuleName,
        [property: JsonPropertyName("contracted_power_kw")] double ContractedPowerKw,
        [property: JsonPropertyName("warning_percent")] double WarningPercent,
        [property: JsonPropertyName("warning_threshold_kw")] double WarningThresholdKw,
        [property: JsonPropertyName("active")] bool Active,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt)
    {
        public static DemandRuleResponse From(DemandRule r) => new(
            r.DemandRuleId.ToString(), r.SiteId.ToString(), r.UserId.ToString(), r.RuleName,
            r.ContractedPowerKw, r.WarningPercent, Math.Round(r.UmbralDeAvisoKw, 2), r.Active,
            r.CreatedAt);
    }

    // -------------------------------------------------------------- respuestas

    public sealed record AlertResponse(
        [property: JsonPropertyName("alert_id")] string AlertId,
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("device_id")] string? DeviceId,
        [property: JsonPropertyName("threshold_id")] string? ThresholdId,
        [property: JsonPropertyName("inactivity_rule_id")] string? InactivityRuleId,
        [property: JsonPropertyName("alert_type")] string? AlertType,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("severity")] string? Severity,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("triggered_at")] DateTime TriggeredAt,
        [property: JsonPropertyName("resolved_at")] DateTime? ResolvedAt)
    {
        public static AlertResponse From(Alert a) => new(a.AlertId.ToString(), a.UserId.ToString(),
            a.DeviceId?.ToString(), a.ThresholdId?.ToString(), a.InactivityRuleId?.ToString(),
            a.AlertType, a.Title, a.Message, a.Severity, a.Status, a.TriggeredAt, a.ResolvedAt);
    }

    public sealed record ThresholdResponse(
        [property: JsonPropertyName("threshold_id")] string ThresholdId,
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("device_id")] string? DeviceId,
        [property: JsonPropertyName("threshold_name")] string? ThresholdName,
        [property: JsonPropertyName("metric")] string? Metric,
        [property: JsonPropertyName("operator")] Operator Operator,
        [property: JsonPropertyName("threshold_value")] double ThresholdValue,
        [property: JsonPropertyName("active")] bool Active)
    {
        public static ThresholdResponse From(AlertThreshold t) => new(t.ThresholdId.ToString(),
            t.UserId.ToString(), t.DeviceId?.ToString(), t.ThresholdName, t.Metric, t.Operator,
            t.ThresholdValue, t.Active);
    }

    public sealed record InactivityRuleResponse(
        [property: JsonPropertyName("inactivity_rule_id")] string InactivityRuleId,
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("device_id")] string? DeviceId,
        [property: JsonPropertyName("rule_name")] string? RuleName,
        [property: JsonPropertyName("max_inactive_minutes")] int MaxInactiveMinutes,
        [property: JsonPropertyName("active")] bool Active)
    {
        public static InactivityRuleResponse From(InactivityRule r) =>
            new(r.InactivityRuleId.ToString(), r.UserId.ToString(), r.DeviceId?.ToString(),
                r.RuleName, r.MaxInactiveMinutes, r.Active);
    }

    public sealed record PreferenceResponse(
        [property: JsonPropertyName("preference_id")] string PreferenceId,
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("channel")] string Channel,
        [property: JsonPropertyName("enabled")] bool Enabled,
        [property: JsonPropertyName("min_severity")] string? MinSeverity,
        [property: JsonPropertyName("quiet_hours_start")] DateTime? QuietHoursStart,
        [property: JsonPropertyName("quiet_hours_end")] DateTime? QuietHoursEnd)
    {
        public static PreferenceResponse From(NotificationPreference p) =>
            new(p.PreferenceId.ToString(), p.UserId.ToString(), p.Channel, p.Enabled,
                p.MinSeverity, p.QuietHoursStart, p.QuietHoursEnd);
    }
}
