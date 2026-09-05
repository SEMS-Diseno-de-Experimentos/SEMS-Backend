namespace Sems.Api.Modules.Analytics.Domain.Model;

/// <summary>
/// Una posicion del ranking de consumo por dispositivo.
///
/// <para>Value object inmutable. Se persiste embebido dentro del ranking, igual
/// que en el documento de MongoDB del servicio original.</para>
/// </summary>
public sealed record RankingItem(int Rank, string DeviceId, string DeviceName, double TotalKwh,
    double EstimatedAmount, double PercentageOfTotal, string Currency);

/// <summary>Proyeccion del recibo de un periodo, calculada a partir del historial.</summary>
public class BillPrediction
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public int PredictionYear { get; private set; }
    public int PredictionMonth { get; private set; }
    public DateTime PeriodStart { get; private set; }
    public DateTime PeriodEnd { get; private set; }
    public double EstimatedKwh { get; private set; }
    public double EstimatedAmount { get; private set; }
    public string Currency { get; private set; } = "PEN";
    public double TariffUsed { get; private set; }
    public double ErrorMarginPercentage { get; private set; }
    public DateTime GeneratedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private BillPrediction()
    {
    }

    public static BillPrediction Create(string userId, int year, int month, DateTime periodStart,
        DateTime periodEnd, double estimatedKwh, double estimatedAmount, string? currency,
        double tariffUsed, double errorMargin)
    {
        var now = DateTime.UtcNow;
        return new BillPrediction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PredictionYear = year,
            PredictionMonth = month,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            EstimatedKwh = estimatedKwh,
            EstimatedAmount = estimatedAmount,
            Currency = string.IsNullOrWhiteSpace(currency) ? "PEN" : currency,
            TariffUsed = tariffUsed,
            ErrorMarginPercentage = errorMargin,
            GeneratedAt = now,
            CreatedAt = now
        };
    }
}

/// <summary>Consejo de ahorro generado para el usuario.</summary>
public class Recommendation
{
    public const string StatusPending = "pending";
    public const string StatusApplied = "applied";

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string? DeviceId { get; private set; }
    public string? RecommendationType { get; private set; }
    public string? Title { get; private set; }
    public string? Description { get; private set; }
    public double EstimatedSavingKwh { get; private set; }
    public double EstimatedSavingAmount { get; private set; }
    public string Currency { get; private set; } = "PEN";
    public string Status { get; private set; } = StatusPending;
    public DateTime GeneratedAt { get; private set; }
    public DateTime? AppliedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Recommendation()
    {
    }

    public static Recommendation Create(string userId, string? deviceId, string? type,
        string? title, string? description, double savingKwh, double savingAmount, string? currency)
    {
        var now = DateTime.UtcNow;
        return new Recommendation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceId = deviceId,
            RecommendationType = type,
            Title = title,
            Description = description,
            EstimatedSavingKwh = savingKwh,
            EstimatedSavingAmount = savingAmount,
            Currency = string.IsNullOrWhiteSpace(currency) ? "PEN" : currency,
            Status = StatusPending,
            GeneratedAt = now,
            CreatedAt = now
        };
    }

    /// <summary>Marcar como aplicada es idempotente: repetirlo no cambia la fecha original.</summary>
    public void Apply()
    {
        if (Status == StatusApplied)
        {
            return;
        }
        Status = StatusApplied;
        AppliedAt = DateTime.UtcNow;
    }
}

/// <summary>Comportamiento de consumo que se aparta de lo esperado.</summary>
public class Anomaly
{
    public const string StatusOpen = "open";
    public const string StatusResolved = "resolved";

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string? DeviceId { get; private set; }
    public string? AnomalyType { get; private set; }
    public string? Description { get; private set; }
    public string? Severity { get; private set; }
    public string Status { get; private set; } = StatusOpen;
    public double ActualKwh { get; private set; }
    public double ExpectedKwh { get; private set; }
    public double DeviationPercentage { get; private set; }
    public DateTime DetectedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Anomaly()
    {
    }

    public static Anomaly Detect(string userId, string? deviceId, string? type, string? description,
        string? severity, double actualKwh, double expectedKwh)
    {
        var now = DateTime.UtcNow;
        // Sin esta guarda, un consumo esperado de cero produciria una division
        // por cero al calcular la desviacion.
        var deviation = expectedKwh == 0 ? 0.0 : (actualKwh - expectedKwh) / expectedKwh * 100.0;

        return new Anomaly
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceId = deviceId,
            AnomalyType = type,
            Description = description,
            Severity = severity,
            Status = StatusOpen,
            ActualKwh = actualKwh,
            ExpectedKwh = expectedKwh,
            DeviationPercentage = deviation,
            DetectedAt = now,
            CreatedAt = now
        };
    }

    public void Resolve()
    {
        if (Status == StatusResolved)
        {
            return;
        }
        Status = StatusResolved;
        ResolvedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Resultado de inferir que tipo de aparato es un dispositivo a partir de su
/// patron de consumo.
/// </summary>
public class DeviceIdentificationResult
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string? DeviceId { get; private set; }
    public string? PredictedDeviceType { get; private set; }
    public double ConfidenceScore { get; private set; }
    public string Status { get; private set; } = "completed";
    public DateTime AnalyzedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private DeviceIdentificationResult()
    {
    }

    public static DeviceIdentificationResult Create(string userId, string? deviceId,
        string? predictedType, double confidence, string? status)
    {
        var now = DateTime.UtcNow;
        return new DeviceIdentificationResult
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceId = deviceId,
            PredictedDeviceType = predictedType,
            ConfidenceScore = confidence,
            Status = string.IsNullOrWhiteSpace(status) ? "completed" : status,
            AnalyzedAt = now,
            CreatedAt = now
        };
    }
}

/// <summary>Ordenacion de los dispositivos de un usuario por consumo en un periodo.</summary>
public class ConsumptionRanking
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string? PeriodType { get; private set; }
    public DateTime PeriodStart { get; private set; }
    public DateTime PeriodEnd { get; private set; }

    /// <summary>
    /// Las posiciones se guardan como JSON en una sola columna, replicando el
    /// documento anidado de MongoDB. Se consultan siempre completas, nunca por
    /// posicion suelta, asi que normalizarlas en otra tabla no aportaria nada.
    /// </summary>
    public string RankingsJson { get; private set; } = "[]";

    public DateTime GeneratedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ConsumptionRanking()
    {
    }

    public static ConsumptionRanking Create(string userId, string? periodType, DateTime periodStart,
        DateTime periodEnd, IEnumerable<RankingItem>? rankings)
    {
        var now = DateTime.UtcNow;
        return new ConsumptionRanking
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PeriodType = periodType,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            RankingsJson = System.Text.Json.JsonSerializer.Serialize(
                rankings?.ToList() ?? new List<RankingItem>()),
            GeneratedAt = now,
            CreatedAt = now
        };
    }

    /// <summary>Una fila con JSON corrupto devuelve un ranking vacio, no una excepcion.</summary>
    public List<RankingItem> Rankings()
    {
        if (string.IsNullOrWhiteSpace(RankingsJson))
        {
            return new List<RankingItem>();
        }
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<RankingItem>>(RankingsJson)
                   ?? new List<RankingItem>();
        }
        catch (System.Text.Json.JsonException)
        {
            return new List<RankingItem>();
        }
    }
}
