package com.sems.energy.domain.model.entities;

import java.time.Instant;
import java.util.UUID;
import lombok.Getter;

/**
 * Resumen del consumo de un dispositivo en un periodo.
 *
 * <p>Guarda los numeros ya agregados para no tener que recorrer miles de
 * lecturas sueltas cada vez que se consulta.
 */
@Getter
public class DeviceConsumption {

    /** Tarifa media de referencia en Peru, la misma del servicio original. */
    public static final double DEFAULT_TARIFF = 0.68;

    private final UUID id;
    private final String userId;
    private final String deviceId;
    private final String deviceName;
    private final String meterId;
    private final double totalKwh;
    private final double costEstimateSoles;
    private final Instant periodStart;
    private final Instant periodEnd;
    private final double peakPowerWatts;
    private final double averagePowerWatts;
    private final int readingCount;
    private final Instant createdAt;
    private final Instant updatedAt;

    private DeviceConsumption(UUID id, String userId, String deviceId, String deviceName,
                              String meterId, double totalKwh, double costEstimateSoles,
                              Instant periodStart, Instant periodEnd, double peakPowerWatts,
                              double averagePowerWatts, int readingCount,
                              Instant createdAt, Instant updatedAt) {
        this.id = id;
        this.userId = userId;
        this.deviceId = deviceId;
        this.deviceName = deviceName;
        this.meterId = meterId;
        this.totalKwh = totalKwh;
        this.costEstimateSoles = costEstimateSoles;
        this.periodStart = periodStart;
        this.periodEnd = periodEnd;
        this.peakPowerWatts = peakPowerWatts;
        this.averagePowerWatts = averagePowerWatts;
        this.readingCount = readingCount;
        this.createdAt = createdAt;
        this.updatedAt = updatedAt;
    }

    public static DeviceConsumption create(String userId, String deviceId, String deviceName,
                                           String meterId, double totalKwh, double costEstimateSoles,
                                           Instant periodStart, Instant periodEnd,
                                           double peakPowerWatts, double averagePowerWatts,
                                           int readingCount) {
        Instant now = Instant.now();
        return new DeviceConsumption(UUID.randomUUID(), userId, deviceId, deviceName, meterId,
                totalKwh, costEstimateSoles, periodStart, periodEnd, peakPowerWatts,
                averagePowerWatts, readingCount, now, now);
    }

    public static DeviceConsumption rehydrate(UUID id, String userId, String deviceId, String deviceName,
                                              String meterId, double totalKwh, double costEstimateSoles,
                                              Instant periodStart, Instant periodEnd, double peakPowerWatts,
                                              double averagePowerWatts, int readingCount,
                                              Instant createdAt, Instant updatedAt) {
        return new DeviceConsumption(id, userId, deviceId, deviceName, meterId, totalKwh,
                costEstimateSoles, periodStart, periodEnd, peakPowerWatts, averagePowerWatts,
                readingCount, createdAt, updatedAt);
    }

    public double costPerKwh(double tariff) {
        return totalKwh * tariff;
    }

    public boolean isHighConsumer(double thresholdKwh) {
        return totalKwh > thresholdKwh;
    }
}
