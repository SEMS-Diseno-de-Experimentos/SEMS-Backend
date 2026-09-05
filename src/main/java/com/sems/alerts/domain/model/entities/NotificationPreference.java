package com.sems.alerts.domain.model.entities;

import java.time.Instant;
import java.util.UUID;
import lombok.Getter;

/** Preferencia de notificacion por canal de un usuario. */
@Getter
public class NotificationPreference {

    public static final String CHANNEL_EMAIL = "email";

    private final UUID preferenceId;
    private final UUID userId;
    private final String channel;
    private final boolean enabled;
    private final String minSeverity;
    private final Instant quietHoursStart;
    private final Instant quietHoursEnd;
    private final Instant createdAt;
    private final Instant updatedAt;

    public NotificationPreference(UUID preferenceId, UUID userId, String channel, boolean enabled,
                                  String minSeverity, Instant quietHoursStart, Instant quietHoursEnd,
                                  Instant createdAt, Instant updatedAt) {
        this.preferenceId = preferenceId;
        this.userId = userId;
        this.channel = channel;
        this.enabled = enabled;
        this.minSeverity = minSeverity;
        this.quietHoursStart = quietHoursStart;
        this.quietHoursEnd = quietHoursEnd;
        this.createdAt = createdAt;
        this.updatedAt = updatedAt;
    }

    public static NotificationPreference create(UUID userId, String channel, boolean enabled,
                                                String minSeverity, Instant quietStart,
                                                Instant quietEnd) {
        Instant now = Instant.now();
        return new NotificationPreference(UUID.randomUUID(), userId, channel, enabled,
                minSeverity, quietStart, quietEnd, now, now);
    }
}
