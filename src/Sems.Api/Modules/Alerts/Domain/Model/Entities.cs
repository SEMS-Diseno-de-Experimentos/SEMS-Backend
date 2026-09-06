using System.Text.Json.Serialization;
using Sems.Api.Shared.Errors;

namespace Sems.Api.Modules.Alerts.Domain.Model;

/// <summary>
/// Comparador de un umbral.
///
/// <para>Se serializa con el simbolo (<c>"&gt;"</c>, <c>"&gt;="</c>...), igual que
/// en el servicio original, porque es lo que el usuario elige en la interfaz.</para>
/// </summary>
[JsonConverter(typeof(OperatorJsonConverter))]
public enum Operator
{
    GREATER_THAN,
    GREATER_THAN_OR_EQUAL,
    LESS_THAN,
    LESS_THAN_OR_EQUAL,
    EQUAL
}

public static class OperatorExtensions
{
    public static string Symbol(this Operator op) => op switch
    {
        Operator.GREATER_THAN => ">",
        Operator.GREATER_THAN_OR_EQUAL => ">=",
        Operator.LESS_THAN => "<",
        Operator.LESS_THAN_OR_EQUAL => "<=",
        Operator.EQUAL => "==",
        _ => throw AppException.Validation("unsupported operator")
    };

    public static Operator ToOperator(string? value)
    {
        var trimmed = value?.Trim();
        return trimmed switch
        {
            ">" => Operator.GREATER_THAN,
            ">=" => Operator.GREATER_THAN_OR_EQUAL,
            "<" => Operator.LESS_THAN,
            "<=" => Operator.LESS_THAN_OR_EQUAL,
            "==" => Operator.EQUAL,
            _ when Enum.TryParse<Operator>(trimmed, ignoreCase: true, out var parsed)
                   && Enum.IsDefined(parsed) => parsed,
            _ => throw AppException.Validation($"unsupported operator: {value}")
        };
    }

    /// <summary>Aplica la comparacion. Es el corazon de la evaluacion de umbrales.</summary>
    public static bool Test(this Operator op, double value, double threshold) => op switch
    {
        Operator.GREATER_THAN => value > threshold,
        Operator.GREATER_THAN_OR_EQUAL => value >= threshold,
        Operator.LESS_THAN => value < threshold,
        Operator.LESS_THAN_OR_EQUAL => value <= threshold,
        Operator.EQUAL => Math.Abs(value - threshold) < double.Epsilon,
        _ => false
    };
}

/// <summary>Serializa el operador como su simbolo, no como el nombre del enum.</summary>
public sealed class OperatorJsonConverter : System.Text.Json.Serialization.JsonConverter<Operator>
{
    public override Operator Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert,
        System.Text.Json.JsonSerializerOptions options) =>
        OperatorExtensions.ToOperator(reader.GetString());

    public override void Write(System.Text.Json.Utf8JsonWriter writer, Operator value,
        System.Text.Json.JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Symbol());
}

/// <summary>Aviso generado para un usuario sobre uno de sus dispositivos.</summary>
public class Alert
{
    public const string StatusActive = "active";
    public const string StatusResolved = "resolved";

    public Guid AlertId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? DeviceId { get; private set; }
    public Guid? ThresholdId { get; private set; }
    public Guid? InactivityRuleId { get; private set; }
    public string? AlertType { get; private set; }
    public string? Title { get; private set; }
    public string? Message { get; private set; }
    public string? Severity { get; private set; }
    public string Status { get; private set; } = StatusActive;
    public DateTime TriggeredAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    private Alert()
    {
    }

    public static Alert Raise(Guid userId, Guid? deviceId, Guid? thresholdId,
        Guid? inactivityRuleId, string? alertType, string? title, string? message,
        string? severity, string? status, DateTime? triggeredAt) => new()
    {
        AlertId = Guid.NewGuid(),
        UserId = userId,
        DeviceId = deviceId,
        ThresholdId = thresholdId,
        InactivityRuleId = inactivityRuleId,
        AlertType = alertType,
        Title = title,
        Message = message,
        Severity = severity,
        Status = string.IsNullOrWhiteSpace(status) ? StatusActive : status,
        TriggeredAt = triggeredAt ?? DateTime.UtcNow
    };

    /// <summary>Al pasar a resuelta se sella la fecha si el cliente no la envio.</summary>
    public void UpdateStatus(string newStatus, DateTime? resolvedAt)
    {
        Status = newStatus;
        ResolvedAt = string.Equals(newStatus, StatusResolved, StringComparison.OrdinalIgnoreCase)
            ? resolvedAt ?? DateTime.UtcNow
            : resolvedAt;
    }
}

/// <summary>Regla que dispara una alerta cuando una metrica cruza un valor.</summary>
public class AlertThreshold
{
    public Guid ThresholdId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? DeviceId { get; private set; }
    public string? ThresholdName { get; private set; }
    public string? Metric { get; private set; }
    public Operator Operator { get; private set; }
    public double ThresholdValue { get; private set; }
    public bool Active { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private AlertThreshold()
    {
    }

    public static AlertThreshold Create(Guid userId, Guid? deviceId, string? name, string? metric,
        Operator op, double value, bool? active)
    {
        var now = DateTime.UtcNow;
        return new AlertThreshold
        {
            ThresholdId = Guid.NewGuid(),
            UserId = userId,
            DeviceId = deviceId,
            ThresholdName = name,
            Metric = metric,
            Operator = op,
            ThresholdValue = value,
            Active = active ?? true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Decide si una lectura rompe este umbral.</summary>
    public bool IsBreachedBy(double value) => Active && Operator.Test(value, ThresholdValue);

    public void Deactivate()
    {
        Active = false;
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>Regla que avisa cuando un dispositivo lleva demasiado tiempo sin reportar.</summary>
public class InactivityRule
{
    public Guid InactivityRuleId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? DeviceId { get; private set; }
    public string? RuleName { get; private set; }
    public int MaxInactiveMinutes { get; private set; }
    public bool Active { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private InactivityRule()
    {
    }

    public static InactivityRule Create(Guid userId, Guid? deviceId, string? ruleName,
        int maxInactiveMinutes, bool active)
    {
        var now = DateTime.UtcNow;
        return new InactivityRule
        {
            InactivityRuleId = Guid.NewGuid(),
            UserId = userId,
            DeviceId = deviceId,
            RuleName = ruleName,
            MaxInactiveMinutes = maxInactiveMinutes,
            Active = active,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Un umbral de cero o negativo desactiva la regla en la practica: sin esa
    /// guarda, cualquier dispositivo estaria siempre inactivo.
    /// </summary>
    public bool IsInactive(DateTime? lastActive, DateTime now)
    {
        if (MaxInactiveMinutes <= 0 || lastActive is null)
        {
            return false;
        }
        return (now - lastActive.Value).TotalMinutes >= MaxInactiveMinutes;
    }
}

/// <summary>Preferencia de notificacion por canal de un usuario.</summary>
public class NotificationPreference
{
    public const string ChannelEmail = "email";

    public Guid PreferenceId { get; private set; }
    public Guid UserId { get; private set; }
    public string Channel { get; private set; } = ChannelEmail;
    public bool Enabled { get; private set; }
    public string? MinSeverity { get; private set; }
    public DateTime? QuietHoursStart { get; private set; }
    public DateTime? QuietHoursEnd { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private NotificationPreference()
    {
    }

    public static NotificationPreference Create(Guid userId, string channel, bool enabled,
        string? minSeverity, DateTime? quietStart, DateTime? quietEnd)
    {
        var now = DateTime.UtcNow;
        return new NotificationPreference
        {
            PreferenceId = Guid.NewGuid(),
            UserId = userId,
            Channel = channel,
            Enabled = enabled,
            MinSeverity = minSeverity,
            QuietHoursStart = quietStart,
            QuietHoursEnd = quietEnd,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(bool enabled, string? minSeverity, DateTime? quietStart, DateTime? quietEnd)
    {
        Enabled = enabled;
        MinSeverity = minSeverity;
        QuietHoursStart = quietStart;
        QuietHoursEnd = quietEnd;
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Registro de cada notificacion enviada.
///
/// <para>Es la evidencia del requisito de trazabilidad de notificaciones: queda
/// constancia del canal, el destinatario, si salio bien y, si no, por que.</para>
/// </summary>
public class NotificationLog
{
    public const string StatusSent = "sent";
    public const string StatusFailed = "failed";

    public Guid NotificationId { get; private set; }
    public Guid? AlertId { get; private set; }
    public string? Channel { get; private set; }
    public string? Recipient { get; private set; }
    public string? Status { get; private set; }
    public DateTime? SentAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private NotificationLog()
    {
    }

    public static NotificationLog Sent(Guid? alertId, string channel, string recipient)
    {
        var now = DateTime.UtcNow;
        return new NotificationLog
        {
            NotificationId = Guid.NewGuid(),
            AlertId = alertId,
            Channel = channel,
            Recipient = recipient,
            Status = StatusSent,
            SentAt = now,
            CreatedAt = now
        };
    }

    public static NotificationLog Failed(Guid? alertId, string channel, string recipient,
        string? errorMessage) => new()
    {
        NotificationId = Guid.NewGuid(),
        AlertId = alertId,
        Channel = channel,
        Recipient = recipient,
        Status = StatusFailed,
        ErrorMessage = errorMessage,
        CreatedAt = DateTime.UtcNow
    };
}

/// <summary>Que tan cerca esta la demanda de la potencia contratada.</summary>
public enum DemandLevel
{
    /// <summary>Por debajo del aviso. No hay nada que decir.</summary>
    OK,

    /// <summary>Se acerca a lo contratado. Aun se puede evitar el recargo.</summary>
    WARNING,

    /// <summary>Ya lo supero. El recargo del mes esta hecho.</summary>
    EXCEEDED
}

/// <summary>
/// Regla que vigila la demanda de un local contra su potencia contratada.
/// </summary>
/// <remarks>
/// <para>Es la alerta que no tenia sentido en el segmento residencial: una
/// vivienda no paga por potencia, asi que su pico de demanda no le cuesta
/// dinero. Un local si, y ademas de una forma cruel: el pico mas alto del mes
/// fija el cargo de los treinta dias, aunque haya durado quince minutos.</para>
///
/// <para>Por eso el aviso llega <b>antes</b> de superar lo contratado y no
/// despues: una vez superado, el recargo del mes ya esta hecho y avisar solo
/// sirve para dar la mala noticia. Con margen, en cambio, todavia se puede
/// apagar algo o retrasar el arranque de un equipo.</para>
/// </remarks>
public class DemandRule
{
    public Guid DemandRuleId { get; private set; }

    public Guid SiteId { get; private set; }

    public Guid UserId { get; private set; }

    public string? RuleName { get; private set; }

    /// <summary>Potencia contratada del local, en kW.</summary>
    public double ContractedPowerKw { get; private set; }

    /// <summary>Porcentaje de lo contratado a partir del cual se avisa.</summary>
    public double WarningPercent { get; private set; }

    public bool Active { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private DemandRule()
    {
    }

    public static DemandRule Create(Guid siteId, Guid userId, string? ruleName,
        double contractedPowerKw, double? warningPercent, bool? active)
    {
        if (siteId == Guid.Empty)
        {
            throw AppException.Validation("site_id is required");
        }
        if (contractedPowerKw <= 0)
        {
            throw AppException.Validation("contracted_power_kw must be greater than zero");
        }

        // Por defecto se avisa al 85%. Deja margen para reaccionar sin generar
        // avisos constantes en un local que normalmente trabaja al 70-80%.
        var aviso = warningPercent ?? 85d;
        if (aviso is <= 0 or > 100)
        {
            throw AppException.Validation("warning_percent must be between 1 and 100");
        }

        var now = DateTime.UtcNow;
        return new DemandRule
        {
            DemandRuleId = Guid.NewGuid(),
            SiteId = siteId,
            UserId = userId,
            RuleName = ruleName,
            ContractedPowerKw = contractedPowerKw,
            WarningPercent = aviso,
            Active = active ?? true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Demanda a la que se dispara el aviso, en kW.</summary>
    public double UmbralDeAvisoKw => ContractedPowerKw * WarningPercent / 100d;

    /// <summary>En que nivel cae una demanda medida.</summary>
    public DemandLevel Evaluar(double demandaKw)
    {
        if (!Active)
        {
            return DemandLevel.OK;
        }
        if (demandaKw > ContractedPowerKw)
        {
            return DemandLevel.EXCEEDED;
        }
        return demandaKw >= UmbralDeAvisoKw ? DemandLevel.WARNING : DemandLevel.OK;
    }

    /// <summary>Cuantos kW quedan hasta superar lo contratado. Negativo si ya se supero.</summary>
    public double MargenKw(double demandaKw) => ContractedPowerKw - demandaKw;

    public void UpdateDetails(string? ruleName, double contractedPowerKw, double warningPercent)
    {
        if (contractedPowerKw <= 0)
        {
            throw AppException.Validation("contracted_power_kw must be greater than zero");
        }
        if (warningPercent is <= 0 or > 100)
        {
            throw AppException.Validation("warning_percent must be between 1 and 100");
        }

        RuleName = ruleName;
        ContractedPowerKw = contractedPowerKw;
        WarningPercent = warningPercent;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        Active = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
