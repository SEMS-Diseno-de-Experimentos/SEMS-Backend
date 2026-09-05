package com.sems.alerts.domain.model.entities;

import com.sems.alerts.domain.model.valueobjects.Operator;
import java.time.Instant;
import java.util.UUID;
import lombok.Getter;

/** Regla que dispara una alerta cuando una metrica cruza un valor. */
@Getter
public class AlertThreshold {

    private final UUID thresholdId;
    private final UUID userId;
    private final UUID deviceId;
    private final String thresholdName;
    private final String metric;
    private final Operator operator;
    private final double thresholdValue;
    private boolean active;
    private final Instant createdAt;
    private Instant updatedAt;

    public AlertThreshold(UUID thresholdId, UUID userId, UUID deviceId, String thresholdName,
                          String metric, Operator operator, double thresholdValue, boolean active,
                          Instant createdAt, Instant updatedAt) {
        this.thresholdId = thresholdId;
        this.userId = userId;
        this.deviceId = deviceId;
        this.thresholdName = thresholdName;
        this.metric = metric;
        this.operator = operator;
        this.thresholdValue = thresholdValue;
        this.active = active;
        this.createdAt = createdAt;
        this.updatedAt = updatedAt;
    }

    public static AlertThreshold create(UUID userId, UUID deviceId, String name, String metric,
                                        Operator operator, double value, Boolean active) {
        Instant now = Instant.now();
        return new AlertThreshold(UUID.randomUUID(), userId, deviceId, name, metric, operator,
                value, active == null || active, now, now);
    }

    /** Decide si una lectura rompe este umbral. */
    public boolean isBreachedBy(double value) {
        return active && operator.test(value, thresholdValue);
    }

    public void deactivate() {
        this.active = false;
        this.updatedAt = Instant.now();
    }
}
