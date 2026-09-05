package com.sems.devices.domain.model.entities;

import com.sems.shared.errors.AppException;
import java.time.Instant;
import java.util.Set;
import java.util.UUID;
import lombok.Getter;

/** Hecho registrado en la vida de un dispositivo. */
@Getter
public class DeviceEvent {

    /** Mismo conjunto cerrado que validaba el servicio en Go. */
    private static final Set<String> ALLOWED_TYPES =
            Set.of("CONNECTED", "DISCONNECTED", "ERROR", "UPDATED", "REMOVED");

    private final UUID eventId;
    private final UUID deviceId;
    private final String eventType;
    private final String description;
    private final Instant occurredAt;

    private DeviceEvent(UUID eventId, UUID deviceId, String eventType,
                        String description, Instant occurredAt) {
        this.eventId = eventId;
        this.deviceId = deviceId;
        this.eventType = eventType;
        this.description = description;
        this.occurredAt = occurredAt;
    }

    public static DeviceEvent create(UUID deviceId, String eventType, String description, Instant occurredAt) {
        if (deviceId == null) {
            throw AppException.validation("device_id is required");
        }
        String normalized = eventType == null ? "" : eventType.trim().toUpperCase();
        if (normalized.isEmpty()) {
            throw AppException.validation("event_type is required");
        }
        if (!ALLOWED_TYPES.contains(normalized)) {
            throw AppException.validation(
                    "event_type must be one of CONNECTED, DISCONNECTED, ERROR, UPDATED or REMOVED");
        }
        return new DeviceEvent(UUID.randomUUID(), deviceId, normalized, description,
                occurredAt != null ? occurredAt : Instant.now());
    }

    public static DeviceEvent rehydrate(UUID eventId, UUID deviceId, String eventType,
                                        String description, Instant occurredAt) {
        return new DeviceEvent(eventId, deviceId, eventType, description, occurredAt);
    }
}
