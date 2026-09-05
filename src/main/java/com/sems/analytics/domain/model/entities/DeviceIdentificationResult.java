package com.sems.analytics.domain.model.entities;

import java.time.Instant;
import java.util.UUID;
import lombok.Getter;

/**
 * Resultado de inferir que tipo de aparato es un dispositivo a partir de su
 * patron de consumo.
 */
@Getter
public class DeviceIdentificationResult {

    private final UUID id;
    private final String userId;
    private final String deviceId;
    private final String predictedDeviceType;
    private final double confidenceScore;
    private final String status;
    private final Instant analyzedAt;
    private final Instant createdAt;

    public DeviceIdentificationResult(UUID id, String userId, String deviceId,
                                      String predictedDeviceType, double confidenceScore,
                                      String status, Instant analyzedAt, Instant createdAt) {
        this.id = id;
        this.userId = userId;
        this.deviceId = deviceId;
        this.predictedDeviceType = predictedDeviceType;
        this.confidenceScore = confidenceScore;
        this.status = status;
        this.analyzedAt = analyzedAt;
        this.createdAt = createdAt;
    }

    public static DeviceIdentificationResult create(String userId, String deviceId,
                                                    String predictedType, double confidence,
                                                    String status) {
        Instant now = Instant.now();
        return new DeviceIdentificationResult(UUID.randomUUID(), userId, deviceId, predictedType,
                confidence, status == null ? "completed" : status, now, now);
    }
}
