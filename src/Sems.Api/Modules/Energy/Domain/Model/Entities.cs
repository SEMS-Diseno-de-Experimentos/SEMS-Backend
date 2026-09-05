using Sems.Api.Shared.Errors;

namespace Sems.Api.Modules.Energy.Domain.Model;

/// <summary>
/// Medidor inteligente que pertenece a un usuario.
///
/// <para>Representa el dispositivo fisico EOS que mide el consumo y produce las
/// lecturas.</para>
/// </summary>
public class EnergyMeter
{
    public Guid Id { get; private set; }

    public string UserId { get; private set; } = string.Empty;

    public string MeterSerial { get; private set; } = string.Empty;

    public string? Model { get; private set; }

    public string? Brand { get; private set; }

    public string? Location { get; private set; }

    public MeterStatus Status { get; private set; }

    public string FirmwareVersion { get; private set; } = "1.0.0";

    public double MaxPowerWatts { get; private set; }

    public DateTime RegisteredAt { get; private set; }

    public DateTime? LastSeenAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private EnergyMeter()
    {
    }

    public static EnergyMeter Register(string? userId, string? meterSerial, string? model,
        string? brand, string? location, string? firmwareVersion, double? maxPowerWatts)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw AppException.Validation("user_id is required");
        }
        if (string.IsNullOrWhiteSpace(meterSerial))
        {
            throw AppException.Validation("meter_serial is required");
        }

        var now = DateTime.UtcNow;
        return new EnergyMeter
        {
            Id = Guid.NewGuid(),
            UserId = userId.Trim(),
            MeterSerial = meterSerial.Trim(),
            Model = model,
            Brand = brand,
            Location = location,
            Status = MeterStatus.active,
            FirmwareVersion = string.IsNullOrWhiteSpace(firmwareVersion) ? "1.0.0" : firmwareVersion,
            MaxPowerWatts = maxPowerWatts ?? 10000.0,
            RegisteredAt = now,
            UpdatedAt = now
        };
    }

    public bool IsActive => Status == MeterStatus.active;

    /// <summary>Marca el instante en que el medidor reporto por ultima vez.</summary>
    public void UpdateLastSeen() => LastSeenAt = DateTime.UtcNow;

    public void Deactivate()
    {
        Status = MeterStatus.inactive;
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Una medicion enviada por un medidor en un instante concreto.
///
/// <para>El conjunto de lecturas forma el historial con el que se calculan
/// consumos, proyecciones y alertas.</para>
/// </summary>
public class EnergyReading
{
    public Guid Id { get; private set; }

    public string UserId { get; private set; } = string.Empty;

    public string MeterId { get; private set; } = string.Empty;

    public string? DeviceId { get; private set; }

    public double PowerWatts { get; private set; }

    public double Voltage { get; private set; }

    public double Current { get; private set; }

    public double Frequency { get; private set; }

    public double EnergyKwh { get; private set; }

    public DateTime Timestamp { get; private set; }

    public string ReadingType { get; private set; } = "real_time";

    public string Phase { get; private set; } = "single";

    public DateTime CreatedAt { get; private set; }

    private EnergyReading()
    {
    }

    public static EnergyReading Record(string? userId, string? meterId, string? deviceId,
        PowerReading measurement, DateTime? timestamp, string? readingType, string? phase)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw AppException.Validation("user_id is required");
        }
        if (string.IsNullOrWhiteSpace(meterId))
        {
            throw AppException.Validation("meter_id is required");
        }

        return new EnergyReading
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MeterId = meterId,
            DeviceId = deviceId,
            PowerWatts = measurement.PowerWatts,
            Voltage = measurement.Voltage,
            Current = measurement.Current,
            Frequency = measurement.Frequency,
            EnergyKwh = measurement.EnergyKwh,
            Timestamp = timestamp ?? DateTime.UtcNow,
            ReadingType = string.IsNullOrWhiteSpace(readingType) ? "real_time" : readingType,
            Phase = string.IsNullOrWhiteSpace(phase) ? "single" : phase,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>Umbral por defecto de 2 kW, el mismo del servicio original.</summary>
    public bool IsHighConsumption(double thresholdWatts = 2000.0) => PowerWatts > thresholdWatts;

    /// <summary>Convierte la potencia instantanea en kWh por hora.</summary>
    public double ToKwhRate() => PowerWatts / 1000.0;
}

/// <summary>
/// Resumen del consumo de un dispositivo en un periodo.
///
/// <para>Guarda los numeros ya agregados para no recorrer miles de lecturas
/// sueltas cada vez que se consulta.</para>
/// </summary>
public class DeviceConsumption
{
    /// <summary>Tarifa media de referencia en Peru, la misma del servicio original.</summary>
    public const double DefaultTariff = 0.68;

    public Guid Id { get; private set; }

    public string UserId { get; private set; } = string.Empty;

    public string DeviceId { get; private set; } = string.Empty;

    public string? DeviceName { get; private set; }

    public string? MeterId { get; private set; }

    public double TotalKwh { get; private set; }

    public double CostEstimateSoles { get; private set; }

    public DateTime PeriodStart { get; private set; }

    public DateTime PeriodEnd { get; private set; }

    public double PeakPowerWatts { get; private set; }

    public double AveragePowerWatts { get; private set; }

    public int ReadingCount { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private DeviceConsumption()
    {
    }

    public static DeviceConsumption Create(string userId, string deviceId, string? deviceName,
        string? meterId, double totalKwh, double costEstimateSoles, DateTime periodStart,
        DateTime periodEnd, double peakPowerWatts, double averagePowerWatts, int readingCount)
    {
        var now = DateTime.UtcNow;
        return new DeviceConsumption
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceId = deviceId,
            DeviceName = deviceName,
            MeterId = meterId,
            TotalKwh = totalKwh,
            CostEstimateSoles = costEstimateSoles,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            PeakPowerWatts = peakPowerWatts,
            AveragePowerWatts = averagePowerWatts,
            ReadingCount = readingCount,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public double CostPerKwh(double tariff = DefaultTariff) => TotalKwh * tariff;

    public bool IsHighConsumer(double thresholdKwh = 100.0) => TotalKwh > thresholdKwh;
}

/// <summary>
/// Aviso generado cuando el consumo rompe una regla.
///
/// <para>Guarda el limite que se vigilaba y el valor real que lo supero, de modo
/// que el mensaje al usuario pueda explicar por que salto.</para>
/// </summary>
public class ConsumptionAlert
{
    public Guid Id { get; private set; }

    public string UserId { get; private set; } = string.Empty;

    public string? DeviceId { get; private set; }

    public string? MeterId { get; private set; }

    public AlertType AlertType { get; private set; }

    public AlertSeverity Severity { get; private set; }

    public double ThresholdValue { get; private set; }

    public double ActualValue { get; private set; }

    public string? Message { get; private set; }

    public bool IsRead { get; private set; }

    public bool IsResolved { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? ResolvedAt { get; private set; }

    private ConsumptionAlert()
    {
    }

    public static ConsumptionAlert Raise(string userId, string? deviceId, string? meterId,
        AlertType alertType, AlertSeverity severity, double thresholdValue, double actualValue,
        string? message) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        DeviceId = deviceId,
        MeterId = meterId,
        AlertType = alertType,
        Severity = severity,
        ThresholdValue = thresholdValue,
        ActualValue = actualValue,
        Message = message,
        IsRead = false,
        IsResolved = false,
        CreatedAt = DateTime.UtcNow
    };

    public void MarkAsRead() => IsRead = true;

    public void Resolve()
    {
        IsResolved = true;
        ResolvedAt = DateTime.UtcNow;
    }

    /// <summary>Cuanto se paso el valor real por encima del limite, en porcentaje.</summary>
    public double ExcessPercentage() =>
        ThresholdValue == 0 ? 0.0 : (ActualValue - ThresholdValue) / ThresholdValue * 100;
}
