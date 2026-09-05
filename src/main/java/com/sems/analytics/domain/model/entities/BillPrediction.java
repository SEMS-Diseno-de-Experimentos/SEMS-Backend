package com.sems.analytics.domain.model.entities;

import java.time.Instant;
import java.util.UUID;
import lombok.Getter;

/** Proyeccion del recibo de un periodo, calculada a partir del historial. */
@Getter
public class BillPrediction {

    private final UUID id;
    private final String userId;
    private final int predictionYear;
    private final int predictionMonth;
    private final Instant periodStart;
    private final Instant periodEnd;
    private final double estimatedKwh;
    private final double estimatedAmount;
    private final String currency;
    private final double tariffUsed;
    private final double errorMarginPercentage;
    private final Instant generatedAt;
    private final Instant createdAt;

    public BillPrediction(UUID id, String userId, int predictionYear, int predictionMonth,
                          Instant periodStart, Instant periodEnd, double estimatedKwh,
                          double estimatedAmount, String currency, double tariffUsed,
                          double errorMarginPercentage, Instant generatedAt, Instant createdAt) {
        this.id = id;
        this.userId = userId;
        this.predictionYear = predictionYear;
        this.predictionMonth = predictionMonth;
        this.periodStart = periodStart;
        this.periodEnd = periodEnd;
        this.estimatedKwh = estimatedKwh;
        this.estimatedAmount = estimatedAmount;
        this.currency = currency;
        this.tariffUsed = tariffUsed;
        this.errorMarginPercentage = errorMarginPercentage;
        this.generatedAt = generatedAt;
        this.createdAt = createdAt;
    }

    public static BillPrediction create(String userId, int year, int month, Instant periodStart,
                                        Instant periodEnd, double estimatedKwh, double estimatedAmount,
                                        String currency, double tariffUsed, double errorMargin) {
        Instant now = Instant.now();
        return new BillPrediction(UUID.randomUUID(), userId, year, month, periodStart, periodEnd,
                estimatedKwh, estimatedAmount, currency == null ? "PEN" : currency,
                tariffUsed, errorMargin, now, now);
    }
}
