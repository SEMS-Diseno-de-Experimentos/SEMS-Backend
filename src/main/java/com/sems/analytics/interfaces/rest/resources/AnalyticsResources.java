package com.sems.analytics.interfaces.rest.resources;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.databind.PropertyNamingStrategies;
import com.fasterxml.jackson.databind.annotation.JsonNaming;
import com.sems.analytics.domain.model.entities.*;
import com.sems.analytics.domain.model.valueobjects.RankingItem;
import jakarta.validation.constraints.NotBlank;
import java.time.Instant;
import java.util.List;

/**
 * Contrato JSON del modulo de analitica.
 *
 * <p>Como el resto de lo que venia de FastAPI, se serializa en snake_case.
 */
public final class AnalyticsResources {

    private AnalyticsResources() {
    }

    // ------------------------------------------------------------- peticiones

    @JsonNaming(PropertyNamingStrategies.SnakeCaseStrategy.class)
    public record CreatePredictionRequest(
            @NotBlank(message = "is required") String userId,
            int predictionYear, int predictionMonth,
            Instant periodStart, Instant periodEnd,
            double estimatedKwh, double estimatedAmount,
            String currency, double tariffUsed, double errorMarginPercentage) {
    }

    @JsonNaming(PropertyNamingStrategies.SnakeCaseStrategy.class)
    public record CreateRecommendationRequest(
            @NotBlank(message = "is required") String userId,
            String deviceId, String recommendationType, String title, String description,
            double estimatedSavingKwh, double estimatedSavingAmount, String currency) {
    }

    @JsonNaming(PropertyNamingStrategies.SnakeCaseStrategy.class)
    public record CreateAnomalyRequest(
            @NotBlank(message = "is required") String userId,
            String deviceId, String anomalyType, String description, String severity,
            double actualKwh, double expectedKwh) {
    }

    @JsonNaming(PropertyNamingStrategies.SnakeCaseStrategy.class)
    public record CreateIdentificationRequest(
            @NotBlank(message = "is required") String userId,
            String deviceId, String predictedDeviceType, double confidenceScore, String status) {
    }

    @JsonNaming(PropertyNamingStrategies.SnakeCaseStrategy.class)
    public record RankingItemResource(int rank, String deviceId, String deviceName, double totalKwh,
                                      double estimatedAmount, double percentageOfTotal, String currency) {

        public static RankingItemResource from(RankingItem i) {
            return new RankingItemResource(i.rank(), i.deviceId(), i.deviceName(), i.totalKwh(),
                    i.estimatedAmount(), i.percentageOfTotal(), i.currency());
        }

        public RankingItem toDomain() {
            return new RankingItem(rank, deviceId, deviceName, totalKwh, estimatedAmount,
                    percentageOfTotal, currency);
        }
    }

    @JsonNaming(PropertyNamingStrategies.SnakeCaseStrategy.class)
    public record CreateRankingRequest(
            @NotBlank(message = "is required") String userId,
            String periodType, Instant periodStart, Instant periodEnd,
            List<RankingItemResource> rankings) {
    }

    // -------------------------------------------------------------- respuestas

    @JsonInclude(JsonInclude.Include.NON_NULL)
    @JsonNaming(PropertyNamingStrategies.SnakeCaseStrategy.class)
    public record BillPredictionResponse(
            String id, String userId, int predictionYear, int predictionMonth,
            Instant periodStart, Instant periodEnd, double estimatedKwh, double estimatedAmount,
            String currency, double tariffUsed, double errorMarginPercentage,
            Instant generatedAt, Instant createdAt) {

        public static BillPredictionResponse from(BillPrediction p) {
            return new BillPredictionResponse(p.getId().toString(), p.getUserId(),
                    p.getPredictionYear(), p.getPredictionMonth(), p.getPeriodStart(),
                    p.getPeriodEnd(), p.getEstimatedKwh(), p.getEstimatedAmount(), p.getCurrency(),
                    p.getTariffUsed(), p.getErrorMarginPercentage(), p.getGeneratedAt(),
                    p.getCreatedAt());
        }
    }

    @JsonInclude(JsonInclude.Include.NON_NULL)
    @JsonNaming(PropertyNamingStrategies.SnakeCaseStrategy.class)
    public record RecommendationResponse(
            String id, String userId, String deviceId, String recommendationType, String title,
            String description, double estimatedSavingKwh, double estimatedSavingAmount,
            String currency, String status, Instant generatedAt, Instant appliedAt, Instant createdAt) {

        public static RecommendationResponse from(Recommendation r) {
            return new RecommendationResponse(r.getId().toString(), r.getUserId(), r.getDeviceId(),
                    r.getRecommendationType(), r.getTitle(), r.getDescription(),
                    r.getEstimatedSavingKwh(), r.getEstimatedSavingAmount(), r.getCurrency(),
                    r.getStatus(), r.getGeneratedAt(), r.getAppliedAt(), r.getCreatedAt());
        }
    }

    @JsonInclude(JsonInclude.Include.NON_NULL)
    @JsonNaming(PropertyNamingStrategies.SnakeCaseStrategy.class)
    public record AnomalyResponse(
            String id, String userId, String deviceId, String anomalyType, String description,
            String severity, String status, double actualKwh, double expectedKwh,
            double deviationPercentage, Instant detectedAt, Instant resolvedAt, Instant createdAt) {

        public static AnomalyResponse from(Anomaly a) {
            return new AnomalyResponse(a.getId().toString(), a.getUserId(), a.getDeviceId(),
                    a.getAnomalyType(), a.getDescription(), a.getSeverity(), a.getStatus(),
                    a.getActualKwh(), a.getExpectedKwh(), a.getDeviationPercentage(),
                    a.getDetectedAt(), a.getResolvedAt(), a.getCreatedAt());
        }
    }

    @JsonInclude(JsonInclude.Include.NON_NULL)
    @JsonNaming(PropertyNamingStrategies.SnakeCaseStrategy.class)
    public record DeviceIdentificationResponse(
            String id, String userId, String deviceId, String predictedDeviceType,
            double confidenceScore, String status, Instant analyzedAt, Instant createdAt) {

        public static DeviceIdentificationResponse from(DeviceIdentificationResult d) {
            return new DeviceIdentificationResponse(d.getId().toString(), d.getUserId(),
                    d.getDeviceId(), d.getPredictedDeviceType(), d.getConfidenceScore(),
                    d.getStatus(), d.getAnalyzedAt(), d.getCreatedAt());
        }
    }

    @JsonInclude(JsonInclude.Include.NON_NULL)
    @JsonNaming(PropertyNamingStrategies.SnakeCaseStrategy.class)
    public record ConsumptionRankingResponse(
            String id, String userId, String periodType, Instant periodStart, Instant periodEnd,
            List<RankingItemResource> rankings, Instant generatedAt, Instant createdAt) {

        public static ConsumptionRankingResponse from(ConsumptionRanking c) {
            return new ConsumptionRankingResponse(c.getId().toString(), c.getUserId(),
                    c.getPeriodType(), c.getPeriodStart(), c.getPeriodEnd(),
                    c.getRankings().stream().map(RankingItemResource::from).toList(),
                    c.getGeneratedAt(), c.getCreatedAt());
        }
    }
}
