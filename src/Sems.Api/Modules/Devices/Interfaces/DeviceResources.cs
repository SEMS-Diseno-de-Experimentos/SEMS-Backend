using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Sems.Api.Modules.Devices.Domain.Model;

namespace Sems.Api.Modules.Devices.Interfaces;

/// <summary>
/// Contrato JSON del modulo de dispositivos.
///
/// <para>Los nombres replican exactamente los del servicio en Go, en camelCase.
/// El frontend no distingue si detras hay Go, Java o C#.</para>
/// </summary>
public static class DeviceResources
{
    // ------------------------------------------------------------- peticiones

    public sealed record CreateDeviceRequest(
        [Required(ErrorMessage = "is required")] string ExternalDeviceCode,
        [Required(ErrorMessage = "is required")] string UserId,
        [Required(ErrorMessage = "is required")] string DeviceName,
        [Required(ErrorMessage = "is required")] string DeviceType,
        string? Brand,
        string? Model,
        [Required(ErrorMessage = "is required")] string ConnectionProtocol);

    public sealed record UpdateDeviceRequest(
        [Required(ErrorMessage = "is required")] string DeviceName,
        [Required(ErrorMessage = "is required")] string DeviceType,
        string? Brand,
        string? Model,
        [Required(ErrorMessage = "is required")] string ConnectionProtocol);

    public sealed record UpdateDeviceStatusRequest(
        [Required(ErrorMessage = "is required")] string Status);

    public sealed record CreateBindingRequest(
        [Required(ErrorMessage = "is required")] string UserId,
        string? HomeId);

    public sealed record CreateConfigurationRequest(
        [Required(ErrorMessage = "is required")] string ConfigKey,
        string? ConfigValue);

    public sealed record UpdateConfigurationRequest(string? ConfigValue);

    public sealed record CreateEventRequest(
        [Required(ErrorMessage = "is required")] string EventType,
        string? Description,
        DateTime? OccurredAt);

    // -------------------------------------------------------------- respuestas

    public sealed record DeviceResource(
        string DeviceId, string ExternalDeviceCode, string UserId, string DeviceName,
        string DeviceType,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Brand,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Model,
        string ConnectionProtocol, string Status, DateTime RegisteredAt, DateTime UpdatedAt)
    {
        public static DeviceResource From(Device d) => new(
            d.DeviceId.ToString(), d.ExternalDeviceCode, d.UserId.ToString(), d.DeviceName,
            d.DeviceType, d.Brand, d.Model, d.ConnectionProtocol.ToString(), d.Status.ToString(),
            d.RegisteredAt, d.UpdatedAt);
    }

    public sealed record DeviceBindingResource(
        string BindingId, string DeviceId, string UserId,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? HomeId,
        string BindingStatus, DateTime LinkedAt,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? UnlinkedAt,
        DateTime UpdatedAt)
    {
        public static DeviceBindingResource From(DeviceBinding b) => new(
            b.BindingId.ToString(), b.DeviceId.ToString(), b.UserId.ToString(),
            b.HomeId?.ToString(), b.BindingStatus.ToString(), b.LinkedAt, b.UnlinkedAt, b.UpdatedAt);
    }

    public sealed record DeviceConfigurationResource(
        string ConfigurationId, string DeviceId, string ConfigKey,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ConfigValue,
        DateTime UpdatedAt)
    {
        public static DeviceConfigurationResource From(DeviceConfiguration c) => new(
            c.ConfigurationId.ToString(), c.DeviceId.ToString(), c.ConfigKey, c.ConfigValue,
            c.UpdatedAt);
    }

    public sealed record DeviceEventResource(
        string EventId, string DeviceId, string EventType,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Description,
        DateTime OccurredAt)
    {
        public static DeviceEventResource From(DeviceEvent e) => new(
            e.EventId.ToString(), e.DeviceId.ToString(), e.EventType, e.Description, e.OccurredAt);
    }
}
