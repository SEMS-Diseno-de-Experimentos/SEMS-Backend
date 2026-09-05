using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Sems.Api.Modules.Energy.Domain.Model;

namespace Sems.Api.Modules.Energy.Interfaces;

/// <summary>
/// Contrato JSON del modulo de energia.
///
/// <para><b>Importante:</b> el servicio original estaba escrito en FastAPI y
/// serializaba en snake_case (<c>user_id</c>, <c>power_watts</c>,
/// <c>meter_serial</c>...). C# usa camelCase por convencion, asi que cada campo
/// declara su nombre real con <c>[JsonPropertyName]</c>. Sin eso el frontend
/// dejaria de encontrar los campos y las pantallas saldrian vacias.</para>
///
/// <para>Se hace campo por campo a proposito: asi el contrato queda escrito en
/// el propio recurso y no depende de una convencion global que alguien pueda
/// cambiar sin darse cuenta.</para>
/// </summary>
public static class EnergyResources
{
    // ------------------------------------------------------------- peticiones

    public sealed record RegisterMeterRequest(
        [property: JsonPropertyName("user_id")]
        [property: Required(ErrorMessage = "is required")] string UserId,
        [property: JsonPropertyName("meter_serial")]
        [property: Required(ErrorMessage = "is required")] string MeterSerial,
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("brand")] string? Brand,
        [property: JsonPropertyName("location")] string? Location,
        [property: JsonPropertyName("firmware_version")] string? FirmwareVersion,
        [property: JsonPropertyName("max_power_watts")] double? MaxPowerWatts);

    public sealed record CreateReadingRequest(
        [property: JsonPropertyName("user_id")]
        [property: Required(ErrorMessage = "is required")] string UserId,
        [property: JsonPropertyName("meter_id")]
        [property: Required(ErrorMessage = "is required")] string MeterId,
        [property: JsonPropertyName("device_id")] string? DeviceId,
        [property: JsonPropertyName("power_watts")] double PowerWatts,
        [property: JsonPropertyName("voltage")] double Voltage,
        [property: JsonPropertyName("current")] double Current,
        [property: JsonPropertyName("frequency")] double Frequency,
        [property: JsonPropertyName("energy_kwh")] double EnergyKwh,
        [property: JsonPropertyName("timestamp")] DateTime? Timestamp,
        [property: JsonPropertyName("reading_type")] string? ReadingType,
        [property: JsonPropertyName("phase")] string? Phase);

    // -------------------------------------------------------------- respuestas

    public sealed record MeterResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("meter_serial")] string MeterSerial,
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("brand")] string? Brand,
        [property: JsonPropertyName("location")] string? Location,
        [property: JsonPropertyName("status")] MeterStatus Status,
        [property: JsonPropertyName("firmware_version")] string FirmwareVersion,
        [property: JsonPropertyName("max_power_watts")] double MaxPowerWatts,
        [property: JsonPropertyName("registered_at")] DateTime RegisteredAt,
        [property: JsonPropertyName("last_seen_at")] DateTime? LastSeenAt,
        [property: JsonPropertyName("updated_at")] DateTime UpdatedAt)
    {
        public static MeterResponse From(EnergyMeter m) => new(
            m.Id.ToString(), m.UserId, m.MeterSerial, m.Model, m.Brand, m.Location, m.Status,
            m.FirmwareVersion, m.MaxPowerWatts, m.RegisteredAt, m.LastSeenAt, m.UpdatedAt);
    }

    public sealed record ReadingResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("meter_id")] string MeterId,
        [property: JsonPropertyName("device_id")] string? DeviceId,
        [property: JsonPropertyName("power_watts")] double PowerWatts,
        [property: JsonPropertyName("voltage")] double Voltage,
        [property: JsonPropertyName("current")] double Current,
        [property: JsonPropertyName("frequency")] double Frequency,
        [property: JsonPropertyName("energy_kwh")] double EnergyKwh,
        [property: JsonPropertyName("timestamp")] DateTime Timestamp,
        [property: JsonPropertyName("reading_type")] string ReadingType,
        [property: JsonPropertyName("phase")] string Phase,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt)
    {
        public static ReadingResponse From(EnergyReading r) => new(
            r.Id.ToString(), r.UserId, r.MeterId, r.DeviceId, r.PowerWatts, r.Voltage, r.Current,
            r.Frequency, r.EnergyKwh, r.Timestamp, r.ReadingType, r.Phase, r.CreatedAt);
    }

    public sealed record ConsumptionResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("device_id")] string DeviceId,
        [property: JsonPropertyName("device_name")] string? DeviceName,
        [property: JsonPropertyName("meter_id")] string? MeterId,
        [property: JsonPropertyName("total_kwh")] double TotalKwh,
        [property: JsonPropertyName("cost_estimate_soles")] double CostEstimateSoles,
        [property: JsonPropertyName("period_start")] DateTime PeriodStart,
        [property: JsonPropertyName("period_end")] DateTime PeriodEnd,
        [property: JsonPropertyName("peak_power_watts")] double PeakPowerWatts,
        [property: JsonPropertyName("average_power_watts")] double AveragePowerWatts,
        [property: JsonPropertyName("reading_count")] int ReadingCount,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt,
        [property: JsonPropertyName("updated_at")] DateTime UpdatedAt)
    {
        public static ConsumptionResponse From(DeviceConsumption c) => new(
            c.Id.ToString(), c.UserId, c.DeviceId, c.DeviceName, c.MeterId, c.TotalKwh,
            c.CostEstimateSoles, c.PeriodStart, c.PeriodEnd, c.PeakPowerWatts,
            c.AveragePowerWatts, c.ReadingCount, c.CreatedAt, c.UpdatedAt);
    }

    public sealed record AlertResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("device_id")] string? DeviceId,
        [property: JsonPropertyName("meter_id")] string? MeterId,
        [property: JsonPropertyName("alert_type")] AlertType AlertType,
        [property: JsonPropertyName("severity")] AlertSeverity Severity,
        [property: JsonPropertyName("threshold_value")] double ThresholdValue,
        [property: JsonPropertyName("actual_value")] double ActualValue,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("is_read")] bool IsRead,
        [property: JsonPropertyName("is_resolved")] bool IsResolved,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt,
        [property: JsonPropertyName("resolved_at")] DateTime? ResolvedAt)
    {
        public static AlertResponse From(ConsumptionAlert a) => new(
            a.Id.ToString(), a.UserId, a.DeviceId, a.MeterId, a.AlertType, a.Severity,
            a.ThresholdValue, a.ActualValue, a.Message, a.IsRead, a.IsResolved, a.CreatedAt,
            a.ResolvedAt);
    }

    public sealed record PricingResponse(
        [property: JsonPropertyName("provider")] string Provider,
        [property: JsonPropertyName("price_per_kwh")] double PricePerKwh,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("timestamp")] DateTime Timestamp)
    {
        public static PricingResponse From(EnergyPrice p) =>
            new(p.Provider, p.PricePerKwh, p.Currency, p.Timestamp);
    }
}
