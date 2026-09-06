using Microsoft.AspNetCore.Mvc;
using Sems.Api.Modules.Analytics.Application;
using Sems.Api.Modules.Analytics.Domain.Model;
using static Sems.Api.Modules.Analytics.Interfaces.AnalyticsResources;

namespace Sems.Api.Modules.Analytics.Interfaces;

/// <summary>
/// API REST del bounded context de analitica.
///
/// <para>Rutas identicas a las del microservicio en FastAPI, bajo el prefijo
/// <c>/api/v1/analytics</c>. El frontend consume sobre todo
/// <c>/bill-predictions/user/{id}</c> y <c>/consumption-rankings/user/{id}</c>.</para>
/// </summary>
[ApiController]
[Route("api/v1/analytics")]
[Tags("Analytics")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly AnalyticsService _service;

    public AnalyticsController(AnalyticsService service) => _service = service;

    // ---------------------------------------------------- bill-predictions

    /// <summary>Proyecciones de recibo de un usuario.</summary>
    [HttpGet("bill-predictions/user/{userId}")]
    public async Task<List<BillPredictionResponse>> Predictions(string userId) =>
        (await _service.PredictionsByUserAsync(userId)).Select(BillPredictionResponse.From).ToList();

    /// <summary>Registra una proyeccion de recibo.</summary>
    [HttpPost("bill-predictions")]
    public async Task<ActionResult<BillPredictionResponse>> CreatePrediction(
        [FromBody] CreatePredictionRequest r)
    {
        var created = await _service.CreatePredictionAsync(r.UserId, r.PredictionYear,
            r.PredictionMonth, r.PeriodStart, r.PeriodEnd, r.EstimatedKwh, r.EstimatedAmount,
            r.Currency, r.TariffUsed, r.ErrorMarginPercentage);
        return StatusCode(StatusCodes.Status201Created, BillPredictionResponse.From(created));
    }

    /// <summary>
    /// Calcula la proyeccion de recibo de un local con la tarifa comercial,
    /// incluido el cargo por potencia.
    /// </summary>
    [HttpPost("bill-predictions/forecast")]
    public async Task<ActionResult<BillPredictionResponse>> ForecastSiteBill(
        [FromBody] ForecastSiteBillRequest r)
    {
        var created = await _service.ForecastSiteBillAsync(r.UserId, r.SiteId, r.TariffCategory,
            r.ContractedPowerKw, r.PredictionYear, r.PredictionMonth, r.PeriodStart, r.PeriodEnd,
            r.KwhPeak, r.KwhOffPeak, r.MaxDemandKw, r.ErrorMarginPercentage);
        return StatusCode(StatusCodes.Status201Created, BillPredictionResponse.From(created));
    }

    // ------------------------------------------------------ recommendations

    /// <summary>Recomendaciones de ahorro de un usuario.</summary>
    [HttpGet("recommendations/user/{userId}")]
    public async Task<List<RecommendationResponse>> Recommendations(string userId) =>
        (await _service.RecommendationsByUserAsync(userId)).Select(RecommendationResponse.From).ToList();

    /// <summary>Registra una recomendacion.</summary>
    [HttpPost("recommendations")]
    public async Task<ActionResult<RecommendationResponse>> CreateRecommendation(
        [FromBody] CreateRecommendationRequest r)
    {
        var created = await _service.CreateRecommendationAsync(r.UserId, r.DeviceId,
            r.RecommendationType, r.Title, r.Description, r.EstimatedSavingKwh,
            r.EstimatedSavingAmount, r.Currency);
        return StatusCode(StatusCodes.Status201Created, RecommendationResponse.From(created));
    }

    /// <summary>Marca una recomendacion como aplicada.</summary>
    [HttpPatch("recommendations/{recommendationId:guid}/apply")]
    public async Task<RecommendationResponse> ApplyRecommendation(Guid recommendationId) =>
        RecommendationResponse.From(await _service.ApplyRecommendationAsync(recommendationId));

    // ------------------------------------------------------------ anomalies

    /// <summary>Anomalias detectadas para un usuario.</summary>
    [HttpGet("anomalies/user/{userId}")]
    public async Task<List<AnomalyResponse>> Anomalies(string userId) =>
        (await _service.AnomaliesByUserAsync(userId)).Select(AnomalyResponse.From).ToList();

    /// <summary>Registra una anomalia.</summary>
    [HttpPost("anomalies")]
    public async Task<ActionResult<AnomalyResponse>> CreateAnomaly([FromBody] CreateAnomalyRequest r)
    {
        var created = await _service.CreateAnomalyAsync(r.UserId, r.DeviceId, r.AnomalyType,
            r.Description, r.Severity, r.ActualKwh, r.ExpectedKwh);
        return StatusCode(StatusCodes.Status201Created, AnomalyResponse.From(created));
    }

    /// <summary>Da por resuelta una anomalia.</summary>
    [HttpPatch("anomalies/{anomalyId:guid}/resolve")]
    public async Task<AnomalyResponse> ResolveAnomaly(Guid anomalyId) =>
        AnomalyResponse.From(await _service.ResolveAnomalyAsync(anomalyId));

    // ------------------------------------------- device-identifications

    /// <summary>Identificaciones de aparatos de un usuario.</summary>
    [HttpGet("device-identifications/user/{userId}")]
    public async Task<List<DeviceIdentificationResponse>> Identifications(string userId) =>
        (await _service.IdentificationsByUserAsync(userId))
        .Select(DeviceIdentificationResponse.From).ToList();

    /// <summary>Registra una identificacion de aparato.</summary>
    [HttpPost("device-identifications")]
    public async Task<ActionResult<DeviceIdentificationResponse>> CreateIdentification(
        [FromBody] CreateIdentificationRequest r)
    {
        var created = await _service.CreateIdentificationAsync(r.UserId, r.DeviceId,
            r.PredictedDeviceType, r.ConfidenceScore, r.Status);
        return StatusCode(StatusCodes.Status201Created, DeviceIdentificationResponse.From(created));
    }

    // -------------------------------------------- consumption-rankings

    /// <summary>Rankings de consumo de un usuario.</summary>
    [HttpGet("consumption-rankings/user/{userId}")]
    public async Task<List<ConsumptionRankingResponse>> Rankings(string userId) =>
        (await _service.RankingsByUserAsync(userId)).Select(ConsumptionRankingResponse.From).ToList();

    /// <summary>Registra un ranking de consumo.</summary>
    [HttpPost("consumption-rankings")]
    public async Task<ActionResult<ConsumptionRankingResponse>> CreateRanking(
        [FromBody] CreateRankingRequest r)
    {
        var items = r.Rankings?.Select(i => i.ToDomain()) ?? Enumerable.Empty<RankingItem>();
        var created = await _service.CreateRankingAsync(r.UserId, r.PeriodType, r.PeriodStart,
            r.PeriodEnd, items);
        return StatusCode(StatusCodes.Status201Created, ConsumptionRankingResponse.From(created));
    }
}
