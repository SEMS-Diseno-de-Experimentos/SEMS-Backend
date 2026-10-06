using Sems.Api.Modules.Analytics.Domain.Model;

namespace Sems.Api.Modules.Analytics.Domain.Repositories;

/// <summary>
/// Puertos de salida del modulo de analitica.
///
/// <para>Los cinco agregados de este contexto se leen siempre igual: por usuario
/// y de lo mas reciente a lo mas antiguo.</para>
/// </summary>
public interface IBillPredictionRepository
{
    Task<BillPrediction> SaveAsync(BillPrediction prediction, CancellationToken ct = default);
    Task<List<BillPrediction>> FindByUserIdAsync(string userId, CancellationToken ct = default);
}

public interface IRecommendationRepository
{
    Task<Recommendation> SaveAsync(Recommendation recommendation, CancellationToken ct = default);
    Task<Recommendation?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Recommendation>> FindByUserIdAsync(string userId, CancellationToken ct = default);
}

public interface IAnomalyRepository
{
    Task<Anomaly> SaveAsync(Anomaly anomaly, CancellationToken ct = default);
    Task<Anomaly?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Anomaly>> FindByUserIdAsync(string userId, CancellationToken ct = default);
}

public interface IDeviceIdentificationRepository
{
    Task<DeviceIdentificationResult> SaveAsync(DeviceIdentificationResult result, CancellationToken ct = default);
    Task<List<DeviceIdentificationResult>> FindByUserIdAsync(string userId, CancellationToken ct = default);
}

public interface IConsumptionRankingRepository
{
    Task<ConsumptionRanking> SaveAsync(ConsumptionRanking ranking, CancellationToken ct = default);
    Task<List<ConsumptionRanking>> FindByUserIdAsync(string userId, CancellationToken ct = default);
}
