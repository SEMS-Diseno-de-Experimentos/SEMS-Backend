package com.sems.energy.infrastructure.persistence.jpa.entities;

import jakarta.persistence.*;
import java.time.Instant;
import java.util.UUID;
import lombok.*;

@Entity
@Table(name = "em_device_consumptions",
        indexes = {
                @Index(name = "idx_em_consumption_user", columnList = "user_id"),
                @Index(name = "idx_em_consumption_device", columnList = "device_id")
        })
@Getter @Setter @NoArgsConstructor @AllArgsConstructor
public class DeviceConsumptionJpaEntity {

    @Id
    @Column(name = "id", nullable = false, updatable = false)
    private UUID id;

    @Column(name = "user_id", nullable = false, length = 80)
    private String userId;

    @Column(name = "device_id", nullable = false, length = 80)
    private String deviceId;

    @Column(name = "device_name", length = 160)
    private String deviceName;

    @Column(name = "meter_id", length = 80)
    private String meterId;

    @Column(name = "total_kwh", nullable = false)
    private double totalKwh;

    @Column(name = "cost_estimate_soles", nullable = false)
    private double costEstimateSoles;

    @Column(name = "period_start", nullable = false)
    private Instant periodStart;

    @Column(name = "period_end", nullable = false)
    private Instant periodEnd;

    @Column(name = "peak_power_watts", nullable = false)
    private double peakPowerWatts;

    @Column(name = "average_power_watts", nullable = false)
    private double averagePowerWatts;

    @Column(name = "reading_count", nullable = false)
    private int readingCount;

    @Column(name = "created_at", nullable = false)
    private Instant createdAt;

    @Column(name = "updated_at", nullable = false)
    private Instant updatedAt;
}
