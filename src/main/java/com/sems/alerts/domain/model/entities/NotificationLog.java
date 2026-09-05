package com.sems.alerts.domain.model.entities;

import java.time.Instant;
import java.util.UUID;
import lombok.Getter;

/**
 * Registro de cada notificacion enviada.
 *
 * <p>Es la evidencia del requisito de trazabilidad de notificaciones: queda
 * constancia del canal, el destinatario, si salio bien y, si no, por que.
 */
@Getter
public class NotificationLog {

    public static final String STATUS_SENT = "sent";
    public static final String STATUS_FAILED = "failed";

    private final UUID notificationId;
    private final UUID alertId;
    private final String channel;
    private final String recipient;
    private final String status;
    private final Instant sentAt;
    private final String errorMessage;
    private final Instant createdAt;

    public NotificationLog(UUID notificationId, UUID alertId, String channel, String recipient,
                           String status, Instant sentAt, String errorMessage, Instant createdAt) {
        this.notificationId = notificationId;
        this.alertId = alertId;
        this.channel = channel;
        this.recipient = recipient;
        this.status = status;
        this.sentAt = sentAt;
        this.errorMessage = errorMessage;
        this.createdAt = createdAt;
    }

    public static NotificationLog sent(UUID alertId, String channel, String recipient) {
        Instant now = Instant.now();
        return new NotificationLog(UUID.randomUUID(), alertId, channel, recipient,
                STATUS_SENT, now, null, now);
    }

    public static NotificationLog failed(UUID alertId, String channel, String recipient,
                                         String errorMessage) {
        return new NotificationLog(UUID.randomUUID(), alertId, channel, recipient,
                STATUS_FAILED, null, errorMessage, Instant.now());
    }
}
