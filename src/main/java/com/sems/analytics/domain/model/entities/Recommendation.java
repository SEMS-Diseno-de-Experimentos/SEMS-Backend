package com.sems.analytics.domain.model.entities;

import java.time.Instant;
import java.util.UUID;
import lombok.Getter;

/** Consejo de ahorro generado para el usuario. */
@Getter
public class Recommendation {

    public static final String STATUS_PENDING = "pending";
    public static final String STATUS_APPLIED = "applied";

    private final UUID id;
    private final String userId;
    private final String deviceId;
    private final String recommendationType;
    private final String title;
    private final String description;
    private final double estimatedSavingKwh;
    private final double estimatedSavingAmount;
    private final String currency;
    private String status;
    private final Instant generatedAt;
    private Instant appliedAt;
    private final Instant createdAt;

    public Recommendation(UUID id, String userId, String deviceId, String recommendationType,
                          String title, String description, double estimatedSavingKwh,
                          double estimatedSavingAmount, String currency, String status,
                          Instant generatedAt, Instant appliedAt, Instant createdAt) {
        this.id = id;
        this.userId = userId;
        this.deviceId = deviceId;
        this.recommendationType = recommendationType;
        this.title = title;
        this.description = description;
        this.estimatedSavingKwh = estimatedSavingKwh;
        this.estimatedSavingAmount = estimatedSavingAmount;
        this.currency = currency;
        this.status = status;
        this.generatedAt = generatedAt;
        this.appliedAt = appliedAt;
        this.createdAt = createdAt;
    }

    public static Recommendation create(String userId, String deviceId, String type, String title,
                                        String description, double savingKwh, double savingAmount,
                                        String currency) {
        Instant now = Instant.now();
        return new Recommendation(UUID.randomUUID(), userId, deviceId, type, title, description,
                savingKwh, savingAmount, currency == null ? "PEN" : currency,
                STATUS_PENDING, now, null, now);
    }

    /** Marcar como aplicada es idempotente: repetirlo no cambia la fecha original. */
    public void apply() {
        if (STATUS_APPLIED.equals(this.status)) {
            return;
        }
        this.status = STATUS_APPLIED;
        this.appliedAt = Instant.now();
    }
}
