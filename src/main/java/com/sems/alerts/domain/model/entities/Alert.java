package com.sems.alerts.domain.model.entities;

import java.time.Instant;
import java.util.UUID;
import lombok.Getter;

/** Aviso generado para un usuario sobre uno de sus dispositivos. */
@Getter
public class Alert {

    public static final String STATUS_ACTIVE = "active";
    public static final String STATUS_RESOLVED = "resolved";

    private final UUID alertId;
    private final UUID userId;
    private final UUID deviceId;
    private final UUID thresholdId;
    private final UUID inactivityRuleId;
    private final String alertType;
    private final String title;
    private final String message;
    private final String severity;
    private String status;
    private final Instant triggeredAt;
    private Instant resolvedAt;

    public Alert(UUID alertId, UUID userId, UUID deviceId, UUID thresholdId, UUID inactivityRuleId,
                 String alertType, String title, String message, String severity, String status,
                 Instant triggeredAt, Instant resolvedAt) {
        this.alertId = alertId;
        this.userId = userId;
        this.deviceId = deviceId;
        this.thresholdId = thresholdId;
        this.inactivityRuleId = inactivityRuleId;
        this.alertType = alertType;
        this.title = title;
        this.message = message;
        this.severity = severity;
        this.status = status;
        this.triggeredAt = triggeredAt;
        this.resolvedAt = resolvedAt;
    }

    public static Alert raise(UUID userId, UUID deviceId, UUID thresholdId, UUID inactivityRuleId,
                              String alertType, String title, String message, String severity,
                              String status, Instant triggeredAt) {
        return new Alert(UUID.randomUUID(), userId, deviceId, thresholdId, inactivityRuleId,
                alertType, title, message, severity,
                status == null || status.isBlank() ? STATUS_ACTIVE : status,
                triggeredAt == null ? Instant.now() : triggeredAt, null);
    }

    /** Al pasar a resuelta se sella la fecha si el cliente no la envio. */
    public void updateStatus(String newStatus, Instant resolvedAt) {
        this.status = newStatus;
        if (STATUS_RESOLVED.equalsIgnoreCase(newStatus)) {
            this.resolvedAt = resolvedAt == null ? Instant.now() : resolvedAt;
        } else {
            this.resolvedAt = resolvedAt;
        }
    }
}
