package com.sems.analytics.domain.model.entities;

import java.time.Instant;
import java.util.UUID;
import lombok.Getter;

/** Comportamiento de consumo que se aparta de lo esperado. */
@Getter
public class Anomaly {

    public static final String STATUS_OPEN = "open";
    public static final String STATUS_RESOLVED = "resolved";

    private final UUID id;
    private final String userId;
    private final String deviceId;
    private final String anomalyType;
    private final String description;
    private final String severity;
    private String status;
    private final double actualKwh;
    private final double expectedKwh;
    private final double deviationPercentage;
    private final Instant detectedAt;
    private Instant resolvedAt;
    private final Instant createdAt;

    public Anomaly(UUID id, String userId, String deviceId, String anomalyType, String description,
                   String severity, String status, double actualKwh, double expectedKwh,
                   double deviationPercentage, Instant detectedAt, Instant resolvedAt,
                   Instant createdAt) {
        this.id = id;
        this.userId = userId;
        this.deviceId = deviceId;
        this.anomalyType = anomalyType;
        this.description = description;
        this.severity = severity;
        this.status = status;
        this.actualKwh = actualKwh;
        this.expectedKwh = expectedKwh;
        this.deviationPercentage = deviationPercentage;
        this.detectedAt = detectedAt;
        this.resolvedAt = resolvedAt;
        this.createdAt = createdAt;
    }

    public static Anomaly detect(String userId, String deviceId, String type, String description,
                                 String severity, double actualKwh, double expectedKwh) {
        Instant now = Instant.now();
        double deviation = expectedKwh == 0 ? 0.0
                : ((actualKwh - expectedKwh) / expectedKwh) * 100.0;
        return new Anomaly(UUID.randomUUID(), userId, deviceId, type, description, severity,
                STATUS_OPEN, actualKwh, expectedKwh, deviation, now, null, now);
    }

    public void resolve() {
        if (STATUS_RESOLVED.equals(this.status)) {
            return;
        }
        this.status = STATUS_RESOLVED;
        this.resolvedAt = Instant.now();
    }
}
