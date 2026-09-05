package com.sems.alerts.domain.model.entities;

import java.time.Duration;
import java.time.Instant;
import java.util.UUID;
import lombok.Getter;

/** Regla que avisa cuando un dispositivo lleva demasiado tiempo sin reportar. */
@Getter
public class InactivityRule {

    private final UUID inactivityRuleId;
    private final UUID userId;
    private final UUID deviceId;
    private final String ruleName;
    private final int maxInactiveMinutes;
    private final boolean active;
    private final Instant createdAt;
    private final Instant updatedAt;

    public InactivityRule(UUID inactivityRuleId, UUID userId, UUID deviceId, String ruleName,
                          int maxInactiveMinutes, boolean active, Instant createdAt,
                          Instant updatedAt) {
        this.inactivityRuleId = inactivityRuleId;
        this.userId = userId;
        this.deviceId = deviceId;
        this.ruleName = ruleName;
        this.maxInactiveMinutes = maxInactiveMinutes;
        this.active = active;
        this.createdAt = createdAt;
        this.updatedAt = updatedAt;
    }

    public static InactivityRule create(UUID userId, UUID deviceId, String ruleName,
                                        int maxInactiveMinutes, boolean active) {
        Instant now = Instant.now();
        return new InactivityRule(UUID.randomUUID(), userId, deviceId, ruleName,
                maxInactiveMinutes, active, now, now);
    }

    /**
     * Un umbral de cero o negativo desactiva la regla en la practica: sin esa
     * guarda, cualquier dispositivo estaria siempre inactivo.
     */
    public boolean isInactive(Instant lastActive, Instant now) {
        if (maxInactiveMinutes <= 0 || lastActive == null) {
            return false;
        }
        return Duration.between(lastActive, now).toMinutes() >= maxInactiveMinutes;
    }
}
