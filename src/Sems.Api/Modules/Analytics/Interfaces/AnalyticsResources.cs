using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Sems.Api.Modules.Analytics.Domain.Model;

namespace Sems.Api.Modules.Analytics.Interfaces;

/// <summary>
/// Contrato JSON del modulo de analitica.
///
/// <para>Como el resto de lo que venia de FastAPI, se serializa en snake_case y
/// cada campo lo declara explicitamente.</para>
/// </summary>
public static class AnalyticsResources
{
    // ------------------------------------------------------------- peticiones

    public sealed record CreatePredictionRequest(
        [property: JsonPropertyName("user_id")]
        [property: Required(ErrorMessage = "is required")] string UserId,
        [property: JsonPropertyName("prediction_year")] int PredictionYear,
        [property: JsonPropertyName("prediction_month")] int PredictionMonth,
        [property: JsonPropertyName("period_start")] DateTime PeriodStart,
        [property: JsonPropertyName("period_end")] DateTime PeriodEnd,
        [property: JsonPropertyName("estimated_kwh")] double EstimatedKwh,
        [property: JsonPropertyName("estimated_amount")] double EstimatedAmount,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("tariff_used")] double TariffUsed,
        [property: JsonPropertyName("error_margin_percentage")] double ErrorMarginPercentage);

    public sealed record CreateRecommendationRequest(
        [property: JsonPropertyName("user_id")]
        [property: Required(ErrorMessage = "is required")] string UserId,
        [property: JsonPropertyName("device_id")] string? DeviceId,
        [property: JsonPropertyName("recommendation_type")] string? RecommendationType,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("estimated_saving_kwh")] double EstimatedSavingKwh,
        [property: JsonPropertyName("estimated_saving_amount")] double EstimatedSavingAmount,
        [property: JsonPropertyName("currency")] string? Currency);

    public sealed record CreateAnomalyRequest(
        [property: JsonPropertyName("user_id")]
        [property: Required(ErrorMessage = "is required")] string UserId,
        [property: JsonPropertyName("device_id")] string? DeviceId,
        [property: JsonPropertyName("anomaly_type")] string? AnomalyType,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("severity")] string? Severity,
        [property: JsonPropertyName("actual_kwh")] double ActualKwh,
        [property: JsonPropertyName("expected_kwh")] double ExpectedKwh);

    public sealed record CreateIdentificationRequest(
        [property: JsonPropertyName("user_id")]
        [property: Required(ErrorMessage = "is required")] string UserId,
        [property: JsonPropertyName("device_id")] string? DeviceId,
        [property: JsonPropertyName("predicted_device_type")] string? PredictedDeviceType,
        [property: JsonPropertyName("confidence_score")] double ConfidenceScore,
        [property: JsonPropertyName("status")] string? Status);

    public sealed record RankingItemResource(
        [property: JsonPropertyName("rank")] int Rank,
        [property: JsonPropertyName("device_id")] string DeviceId,
        [property: JsonPropertyName("device_name")] string DeviceName,
        [property: JsonPropertyName("total_kwh")] double TotalKwh,
        [property: JsonPropertyName("estimated_amount")] double EstimatedAmount,
        [property: JsonPropertyName("percentage_of_total")] double PercentageOfTotal,
        [property: JsonPropertyName("currency")] string Currency)
    {
        public static RankingItemResource From(RankingItem i) => new(i.Rank, i.DeviceId,
            i.DeviceName, i.TotalKwh, i.EstimatedAmount, i.PercentageOfTotal, i.Currency);

        public RankingItem ToDomain() => new(Rank, DeviceId, DeviceName, TotalKwh, EstimatedAmount,
            PercentageOfTotal, Currency);
    }

    public sealed record CreateRankingRequest(
        [property: JsonPropertyName("user_id")]
        [property: Required(ErrorMessage = "is required")] string UserId,
        [property: JsonPropertyName("period_type")] string? PeriodType,
        [property: JsonPropertyName("period_start")] DateTime PeriodStart,
        [property: JsonPropertyName("period_end")] DateTime PeriodEnd,
        [property: JsonPropertyName("rankings")] List<RankingItemResource>? Rankings);

    // -------------------------------------------------------------- respuestas

    public sealed record BillPredictionResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("prediction_year")] int PredictionYear,
        [property: JsonPropertyName("prediction_month")] int PredictionMonth,
        [property: JsonPropertyName("period_start")] DateTime PeriodStart,
        [property: JsonPropertyName("period_end")] DateTime PeriodEnd,
        [property: JsonPropertyName("estimated_kwh")] double EstimatedKwh,
        [property: JsonPropertyName("estimated_amount")] double EstimatedAmount,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("tariff_used")] double TariffUsed,
        [property: JsonPropertyName("error_margin_percentage")] double ErrorMarginPercentage,
        [property: JsonPropertyName("generated_at")] DateTime GeneratedAt,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt)
    {
        public static BillPredictionResponse From(BillPrediction p) => new(p.Id.ToString(),
            p.UserId, p.PredictionYear, p.PredictionMonth, p.PeriodStart, p.PeriodEnd,
            p.EstimatedKwh, p.EstimatedAmount, p.Currency, p.TariffUsed, p.ErrorMarginPercentage,
            p.GeneratedAt, p.CreatedAt);
    }

    public sealed record RecommendationResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("device_id")] string? DeviceId,
        [property: JsonPropertyName("recommendation_type")] string? RecommendationType,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("estimated_saving_kwh")] double EstimatedSavingKwh,
        [property: JsonPropertyName("estimated_saving_amount")] double EstimatedSavingAmount,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("generated_at")] DateTime GeneratedAt,
        [property: JsonPropertyName("applied_at")] DateTime? AppliedAt,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt)
    {
        public static RecommendationResponse From(Recommendation r) => new(r.Id.ToString(),
            r.UserId, r.DeviceId, r.RecommendationType, r.Title, r.Description,
            r.EstimatedSavingKwh, r.EstimatedSavingAmount, r.Currency, r.Status, r.GeneratedAt,
            r.AppliedAt, r.CreatedAt);
    }

    public sealed record AnomalyResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("device_id")] string? DeviceId,
        [property: JsonPropertyName("anomaly_type")] string? AnomalyType,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("severity")] string? Severity,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("actual_kwh")] double ActualKwh,
        [property: JsonPropertyName("expected_kwh")] double ExpectedKwh,
        [property: JsonPropertyName("deviation_percentage")] double DeviationPercentage,
        [property: JsonPropertyName("detected_at")] DateTime DetectedAt,
        [property: JsonPropertyName("resolved_at")] DateTime? ResolvedAt,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt)
    {
        public static AnomalyResponse From(Anomaly a) => new(a.Id.ToString(), a.UserId, a.DeviceId,
            a.AnomalyType, a.Description, a.Severity, a.Status, a.ActualKwh, a.ExpectedKwh,
            a.DeviationPercentage, a.DetectedAt, a.ResolvedAt, a.CreatedAt);
    }

    public sealed record DeviceIdentificationResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("device_id")] string? DeviceId,
        [property: JsonPropertyName("predicted_device_type")] string? PredictedDeviceType,
        [property: JsonPropertyName("confidence_score")] double ConfidenceScore,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("analyzed_at")] DateTime AnalyzedAt,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt)
    {
        public static DeviceIdentificationResponse From(DeviceIdentificationResult d) =>
            new(d.Id.ToString(), d.UserId, d.DeviceId, d.PredictedDeviceType, d.ConfidenceScore,
                d.Status, d.AnalyzedAt, d.CreatedAt);
    }

    public sealed record ConsumptionRankingResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("period_type")] string? PeriodType,
        [property: JsonPropertyName("period_start")] DateTime PeriodStart,
        [property: JsonPropertyName("period_end")] DateTime PeriodEnd,
        [property: JsonPropertyName("rankings")] List<RankingItemResource> Rankings,
        [property: JsonPropertyName("generated_at")] DateTime GeneratedAt,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt)
    {
        public static ConsumptionRankingResponse From(ConsumptionRanking c) => new(c.Id.ToString(),
            c.UserId, c.PeriodType, c.PeriodStart, c.PeriodEnd,
            c.Rankings().Select(RankingItemResource.From).ToList(), c.GeneratedAt, c.CreatedAt);
    }
}
