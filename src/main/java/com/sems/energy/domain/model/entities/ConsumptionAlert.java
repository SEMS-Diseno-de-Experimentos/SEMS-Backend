package com.sems.energy.domain.model.entities;

import com.sems.energy.domain.model.valueobjects.AlertSeverity;
import com.sems.energy.domain.model.valueobjects.AlertType;
import java.time.Instant;
import java.util.UUID;
import lombok.Getter;

/**
 * Aviso generado cuando el consumo rompe una regla.
 *
 * <p>Guarda el limite que se vigilaba y el valor real que lo supero, de modo
 * que el mensaje al usuario pueda explicar por que salto.
 */
@Getter
public class ConsumptionAlert {

    private final UUID id;
    private final String userId;
    private final String deviceId;
    private final String meterId;
    private final AlertType alertType;
    private final AlertSeverity severity;
    private final double thresholdValue;
    private final double actualValue;
    private final String message;
    private boolean read;
    private boolean resolved;
    private final Instant createdAt;
    private Instant resolvedAt;

    private ConsumptionAlert(UUID id, String userId, String deviceId, String meterId,
                             AlertType alertType, AlertSeverity severity, double thresholdValue,
                             double actualValue, String message, boolean read, boolean resolved,
                             Instant createdAt, Instant resolvedAt) {
        this.id = id;
        this.userId = userId;
        this.deviceId = deviceId;
        this.meterId = meterId;
        this.alertType = alertType;
        this.severity = severity;
        this.thresholdValue = thresholdValue;
        this.actualValue = actualValue;
        this.message = message;
        this.read = read;
        this.resolved = resolved;
        this.createdAt = createdAt;
        this.resolvedAt = resolvedAt;
    }

    public static ConsumptionAlert raise(String userId, String deviceId, String meterId,
                                         AlertType alertType, AlertSeverity severity,
                                         double thresholdValue, double actualValue, String message) {
        return new ConsumptionAlert(UUID.randomUUID(), userId, deviceId, meterId, alertType,
                severity, thresholdValue, actualValue, message, false, false, Instant.now(), null);
    }

    public static ConsumptionAlert rehydrate(UUID id, String userId, String deviceId, String meterId,
                                             AlertType alertType, AlertSeverity severity,
                                             double thresholdValue, double actualValue, String message,
                                             boolean read, boolean resolved, Instant createdAt,
                                             Instant resolvedAt) {
        return new ConsumptionAlert(id, userId, deviceId, meterId, alertType, severity,
                thresholdValue, actualValue, message, read, resolved, createdAt, resolvedAt);
    }

    public void markAsRead() {
        this.read = true;
    }

    public void resolve() {
        this.resolved = true;
        this.resolvedAt = Instant.now();
    }

    /** Cuanto se paso el valor real por encima del limite, en porcentaje. */
    public double excessPercentage() {
        if (thresholdValue == 0) {
            return 0.0;
        }
        return ((actualValue - thresholdValue) / thresholdValue) * 100;
    }
}
