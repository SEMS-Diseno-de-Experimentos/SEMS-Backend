using Sems.Api.Shared.Errors;

namespace Sems.Api.Modules.Devices.Domain.Model;

/// <summary>Vinculo entre un dispositivo y el usuario u hogar que lo utiliza.</summary>
public class DeviceBinding
{
    public Guid BindingId { get; private set; }

    public Guid DeviceId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid? HomeId { get; private set; }

    public BindingStatus BindingStatus { get; private set; }

    public DateTime LinkedAt { get; private set; }

    public DateTime? UnlinkedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private DeviceBinding()
    {
    }

    public static DeviceBinding Create(Guid deviceId, Guid userId, Guid? homeId)
    {
        if (deviceId == Guid.Empty)
        {
            throw AppException.Validation("device_id is required");
        }
        if (userId == Guid.Empty)
        {
            throw AppException.Validation("user_id is required");
        }

        var now = DateTime.UtcNow;
        return new DeviceBinding
        {
            BindingId = Guid.NewGuid(),
            DeviceId = deviceId,
            UserId = userId,
            HomeId = homeId,
            BindingStatus = BindingStatus.LINKED,
            LinkedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Desvincular dos veces es un conflicto, no una operacion idempotente.</summary>
    public void Unlink()
    {
        if (BindingStatus == BindingStatus.UNLINKED)
        {
            throw AppException.Conflict("binding is already unlinked");
        }
        var now = DateTime.UtcNow;
        BindingStatus = BindingStatus.UNLINKED;
        UnlinkedAt = now;
        UpdatedAt = now;
    }
}

/// <summary>Ajuste con nombre asociado a un dispositivo.</summary>
public class DeviceConfiguration
{
    public Guid ConfigurationId { get; private set; }

    public Guid DeviceId { get; private set; }

    public string ConfigKey { get; private set; } = string.Empty;

    public string? ConfigValue { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private DeviceConfiguration()
    {
    }

    public static DeviceConfiguration Create(Guid deviceId, string? key, string? value)
    {
        if (deviceId == Guid.Empty)
        {
            throw AppException.Validation("device_id is required");
        }
        if (string.IsNullOrWhiteSpace(key))
        {
            throw AppException.Validation("config_key is required");
        }

        return new DeviceConfiguration
        {
            ConfigurationId = Guid.NewGuid(),
            DeviceId = deviceId,
            ConfigKey = key.Trim(),
            ConfigValue = value,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(string? value)
    {
        ConfigValue = value;
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>Hecho registrado en la vida de un dispositivo.</summary>
public class DeviceEvent
{
    /// <summary>Mismo conjunto cerrado que validaba el servicio en Go.</summary>
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "CONNECTED", "DISCONNECTED", "ERROR", "UPDATED", "REMOVED"
    };

    public Guid EventId { get; private set; }

    public Guid DeviceId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateTime OccurredAt { get; private set; }

    private DeviceEvent()
    {
    }

    public static DeviceEvent Create(Guid deviceId, string? eventType, string? description,
        DateTime? occurredAt)
    {
        if (deviceId == Guid.Empty)
        {
            throw AppException.Validation("device_id is required");
        }

        var normalized = eventType?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw AppException.Validation("event_type is required");
        }
        if (!AllowedTypes.Contains(normalized))
        {
            throw AppException.Validation(
                "event_type must be one of CONNECTED, DISCONNECTED, ERROR, UPDATED or REMOVED");
        }

        return new DeviceEvent
        {
            EventId = Guid.NewGuid(),
            DeviceId = deviceId,
            EventType = normalized,
            Description = description,
            OccurredAt = occurredAt ?? DateTime.UtcNow
        };
    }
}
