package com.sems.devices.domain.model.entities;

import com.sems.shared.errors.AppException;
import java.time.Instant;
import java.util.UUID;
import lombok.Getter;

/** Ajuste con nombre asociado a un dispositivo. */
@Getter
public class DeviceConfiguration {

    private final UUID configurationId;
    private final UUID deviceId;
    private final String configKey;
    private String configValue;
    private Instant updatedAt;

    private DeviceConfiguration(UUID configurationId, UUID deviceId, String configKey,
                                String configValue, Instant updatedAt) {
        this.configurationId = configurationId;
        this.deviceId = deviceId;
        this.configKey = configKey;
        this.configValue = configValue;
        this.updatedAt = updatedAt;
    }

    public static DeviceConfiguration create(UUID deviceId, String key, String value) {
        if (deviceId == null) {
            throw AppException.validation("device_id is required");
        }
        if (key == null || key.trim().isEmpty()) {
            throw AppException.validation("config_key is required");
        }
        return new DeviceConfiguration(UUID.randomUUID(), deviceId, key.trim(), value, Instant.now());
    }

    public static DeviceConfiguration rehydrate(UUID configurationId, UUID deviceId, String key,
                                                String value, Instant updatedAt) {
        return new DeviceConfiguration(configurationId, deviceId, key, value, updatedAt);
    }

    public void update(String value) {
        this.configValue = value;
        this.updatedAt = Instant.now();
    }
}
