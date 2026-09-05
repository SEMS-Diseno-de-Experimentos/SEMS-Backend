package com.sems.energy.domain.model.entities;

import com.sems.energy.domain.model.valueobjects.PowerReading;
import com.sems.shared.errors.AppException;
import java.time.Instant;
import java.util.UUID;
import lombok.Getter;

/**
 * Una medicion enviada por un medidor en un instante concreto.
 *
 * <p>El conjunto de lecturas forma el historial con el que se calculan
 * consumos, proyecciones y alertas.
 */
@Getter
public class EnergyReading {

    private final UUID id;
    private final String userId;
    private final String meterId;
    private final String deviceId;
    private final PowerReading measurement;
    private final Instant timestamp;
    private final String readingType;
    private final String phase;
    private final Instant createdAt;

    private EnergyReading(UUID id, String userId, String meterId, String deviceId,
                          PowerReading measurement, Instant timestamp, String readingType,
                          String phase, Instant createdAt) {
        this.id = id;
        this.userId = userId;
        this.meterId = meterId;
        this.deviceId = deviceId;
        this.measurement = measurement;
        this.timestamp = timestamp;
        this.readingType = readingType;
        this.phase = phase;
        this.createdAt = createdAt;
    }

    public static EnergyReading record(String userId, String meterId, String deviceId,
                                       PowerReading measurement, Instant timestamp,
                                       String readingType, String phase) {
        if (userId == null || userId.isBlank()) {
            throw AppException.validation("user_id is required");
        }
        if (meterId == null || meterId.isBlank()) {
            throw AppException.validation("meter_id is required");
        }
        return new EnergyReading(UUID.randomUUID(), userId, meterId, deviceId, measurement,
                timestamp == null ? Instant.now() : timestamp,
                readingType == null ? "real_time" : readingType,
                phase == null ? "single" : phase,
                Instant.now());
    }

    public static EnergyReading rehydrate(UUID id, String userId, String meterId, String deviceId,
                                          PowerReading measurement, Instant timestamp,
                                          String readingType, String phase, Instant createdAt) {
        return new EnergyReading(id, userId, meterId, deviceId, measurement, timestamp,
                readingType, phase, createdAt);
    }

    /** Umbral por defecto de 2 kW, el mismo del servicio original. */
    public boolean isHighConsumption(double thresholdWatts) {
        return measurement.powerWatts() > thresholdWatts;
    }

    public boolean isHighConsumption() {
        return isHighConsumption(2000.0);
    }

    /** Convierte la potencia instantanea en kWh por hora. */
    public double toKwhRate() {
        return measurement.powerWatts() / 1000.0;
    }
}
