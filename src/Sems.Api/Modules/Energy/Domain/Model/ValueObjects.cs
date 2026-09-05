using System.Text.Json.Serialization;
using Sems.Api.Shared.Errors;

namespace Sems.Api.Modules.Energy.Domain.Model;

/// <summary>
/// Estado del medidor.
///
/// <para>El servicio en Python serializaba estos valores en minusculas
/// (<c>"active"</c>, <c>"inactive"</c>...). El convertidor de abajo conserva ese
/// formato exacto para no romper al frontend.</para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MeterStatus>))]
public enum MeterStatus
{
    active,
    inactive,
    maintenance,
    error
}

/// <summary>Motivo por el que se genero una alerta de consumo.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AlertType>))]
public enum AlertType
{
    high_consumption,
    anomaly_detected,
    device_always_on,
    threshold_exceeded,
    unusual_pattern
}

/// <summary>Urgencia de la alerta, de menor a mayor.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AlertSeverity>))]
public enum AlertSeverity
{
    low,
    medium,
    high,
    critical
}

public static class EnergyEnums
{
    public static MeterStatus ToMeterStatus(string? value) =>
        Enum.TryParse<MeterStatus>(value?.Trim(), ignoreCase: true, out var s) && Enum.IsDefined(s)
            ? s
            : throw AppException.Validation("invalid meter status");

    public static AlertType ToAlertType(string? value) =>
        Enum.TryParse<AlertType>(value?.Trim(), ignoreCase: true, out var t) && Enum.IsDefined(t)
            ? t
            : throw AppException.Validation("invalid alert type");

    public static AlertSeverity ToAlertSeverity(string? value) =>
        Enum.TryParse<AlertSeverity>(value?.Trim(), ignoreCase: true, out var s) && Enum.IsDefined(s)
            ? s
            : throw AppException.Validation("invalid alert severity");
}

/// <summary>
/// Medicion electrica instantanea. Value object inmutable.
///
/// <para>Las validaciones son las mismas que hacia <c>__post_init__</c> en
/// Python: ninguna magnitud puede ser negativa y la frecuencia tiene que estar
/// en el rango fisico de una red electrica.</para>
/// </summary>
public sealed record PowerReading
{
    public double PowerWatts { get; }

    public double Voltage { get; }

    public double Current { get; }

    public double Frequency { get; }

    public double EnergyKwh { get; }

    public PowerReading(double powerWatts, double voltage, double current, double frequency,
        double energyKwh)
    {
        if (powerWatts < 0)
        {
            throw AppException.Validation("Power watts cannot be negative.");
        }
        if (voltage < 0)
        {
            throw AppException.Validation("Voltage cannot be negative.");
        }
        if (current < 0)
        {
            throw AppException.Validation("Current cannot be negative.");
        }
        if (frequency is < 45.0 or > 65.0)
        {
            throw AppException.Validation("Frequency must be between 45 and 65 Hz.");
        }

        PowerWatts = powerWatts;
        Voltage = voltage;
        Current = current;
        Frequency = frequency;
        EnergyKwh = energyKwh;
    }

    /// <summary>Potencia aparente en voltiamperios.</summary>
    public double ApparentPowerVa() => Voltage * Current;

    /// <summary>Factor de potencia, acotado a 1.</summary>
    public double PowerFactor()
    {
        var apparent = ApparentPowerVa();
        return apparent == 0 ? 1.0 : Math.Min(PowerWatts / apparent, 1.0);
    }
}

/// <summary>Precio de la electricidad publicado por el proveedor externo.</summary>
public sealed record EnergyPrice(string Provider, double PricePerKwh, string Currency,
    DateTime Timestamp);
