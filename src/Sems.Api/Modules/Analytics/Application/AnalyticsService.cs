using Sems.Api.Modules.Analytics.Domain.Model;
using Sems.Api.Modules.Analytics.Domain.Repositories;
using Sems.Api.Shared.Errors;

namespace Sems.Api.Modules.Analytics.Application;

/// <summary>
/// Casos de uso del modulo de analitica.
///
/// <para>Comandos y consultas conviven aqui porque el contexto es pequenio:
/// cinco agregados con operaciones de crear, listar y cerrar. Separarlos en dos
/// servicios anadiria ceremonia sin aportar claridad.</para>
/// </summary>
public sealed class AnalyticsService
{
    private readonly IBillPredictionRepository _predictions;
    private readonly IRecommendationRepository _recommendations;
    private readonly IAnomalyRepository _anomalies;
    private readonly IDeviceIdentificationRepository _identifications;
    private readonly IConsumptionRankingRepository _rankings;

    public AnalyticsService(IBillPredictionRepository predictions,
        IRecommendationRepository recommendations, IAnomalyRepository anomalies,
        IDeviceIdentificationRepository identifications, IConsumptionRankingRepository rankings)
    {
        _predictions = predictions;
        _recommendations = recommendations;
        _anomalies = anomalies;
        _identifications = identifications;
        _rankings = rankings;
    }

    // -------------------------------------------------- proyeccion de recibo

    public Task<BillPrediction> CreatePredictionAsync(string userId, int year, int month,
        DateTime periodStart, DateTime periodEnd, double estimatedKwh, double estimatedAmount,
        string? currency, double tariffUsed, double errorMargin, CancellationToken ct = default) =>
        _predictions.SaveAsync(BillPrediction.Create(userId, year, month, periodStart, periodEnd,
            estimatedKwh, estimatedAmount, currency, tariffUsed, errorMargin), ct);

    public Task<List<BillPrediction>> PredictionsByUserAsync(string userId, CancellationToken ct = default) =>
        _predictions.FindByUserIdAsync(userId, ct);

    // -------------------------------------------------------- recomendaciones

    public Task<Recommendation> CreateRecommendationAsync(string userId, string? deviceId,
        string? type, string? title, string? description, double savingKwh, double savingAmount,
        string? currency, CancellationToken ct = default) =>
        _recommendations.SaveAsync(Recommendation.Create(userId, deviceId, type, title, description,
            savingKwh, savingAmount, currency), ct);

    public Task<List<Recommendation>> RecommendationsByUserAsync(string userId,
        CancellationToken ct = default) => _recommendations.FindByUserIdAsync(userId, ct);

    public async Task<Recommendation> ApplyRecommendationAsync(Guid id, CancellationToken ct = default)
    {
        var recommendation = await _recommendations.FindByIdAsync(id, ct)
                             ?? throw AppException.NotFound($"Recommendation '{id}' not found");
        recommendation.Apply();
        return await _recommendations.SaveAsync(recommendation, ct);
    }

    // ------------------------------------------------------------- anomalias

    public Task<Anomaly> CreateAnomalyAsync(string userId, string? deviceId, string? type,
        string? description, string? severity, double actualKwh, double expectedKwh,
        CancellationToken ct = default) =>
        _anomalies.SaveAsync(Anomaly.Detect(userId, deviceId, type, description, severity,
            actualKwh, expectedKwh), ct);

    public Task<List<Anomaly>> AnomaliesByUserAsync(string userId, CancellationToken ct = default) =>
        _anomalies.FindByUserIdAsync(userId, ct);

    public async Task<Anomaly> ResolveAnomalyAsync(Guid id, CancellationToken ct = default)
    {
        var anomaly = await _anomalies.FindByIdAsync(id, ct)
                      ?? throw AppException.NotFound($"Anomaly '{id}' not found");
        anomaly.Resolve();
        return await _anomalies.SaveAsync(anomaly, ct);
    }

    // ---------------------------------------- identificacion de aparatos

    public Task<DeviceIdentificationResult> CreateIdentificationAsync(string userId,
        string? deviceId, string? predictedType, double confidence, string? status,
        CancellationToken ct = default) =>
        _identifications.SaveAsync(DeviceIdentificationResult.Create(userId, deviceId,
            predictedType, confidence, status), ct);

    public Task<List<DeviceIdentificationResult>> IdentificationsByUserAsync(string userId,
        CancellationToken ct = default) => _identifications.FindByUserIdAsync(userId, ct);

    // ----------------------------------------------------------------- ranking

    public Task<ConsumptionRanking> CreateRankingAsync(string userId, string? periodType,
        DateTime periodStart, DateTime periodEnd, IEnumerable<RankingItem>? items,
        CancellationToken ct = default) =>
        _rankings.SaveAsync(ConsumptionRanking.Create(userId, periodType, periodStart, periodEnd,
            items), ct);

    public Task<List<ConsumptionRanking>> RankingsByUserAsync(string userId,
        CancellationToken ct = default) => _rankings.FindByUserIdAsync(userId, ct);
}
