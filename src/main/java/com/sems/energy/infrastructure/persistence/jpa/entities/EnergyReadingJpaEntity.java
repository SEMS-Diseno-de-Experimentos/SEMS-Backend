package com.sems.energy.infrastructure.persistence.jpa.entities;

import jakarta.persistence.*;
import java.time.Instant;
import java.util.UUID;
import lombok.*;

/**
 * Lectura del medidor.
 *
 * <p>Sustituye a la coleccion de MongoDB. El indice compuesto por dispositivo y
 * marca de tiempo es el que sostiene las consultas de historial y de ultima
 * lectura, que son las mas frecuentes.
 */
@Entity
@Table(name = "em_energy_readings",
        indexes = {
                @Index(name = "idx_em_readings_user_ts", columnList = "user_id, timestamp"),
                @Index(name = "idx_em_readings_device_ts", columnList = "device_id, timestamp"),
                @Index(name = "idx_em_readings_meter_ts", columnList = "meter_id, timestamp")
        })
@Getter @Setter @NoArgsConstructor @AllArgsConstructor
public class EnergyReadingJpaEntity {

    @Id
    @Column(name = "id", nullable = false, updatable = false)
    private UUID id;

    @Column(name = "user_id", nullable = false, length = 80)
    private String userId;

    @Column(name = "meter_id", nullable = false, length = 80)
    private String meterId;

    @Column(name = "device_id", length = 80)
    private String deviceId;

    @Column(name = "power_watts", nullable = false)
    private double powerWatts;

    @Column(name = "voltage", nullable = false)
    private double voltage;

    @Column(name = "current_amperes", nullable = false)
    private double current;

    @Column(name = "frequency", nullable = false)
    private double frequency;

    @Column(name = "energy_kwh", nullable = false)
    private double energyKwh;

    @Column(name = "timestamp", nullable = false)
    private Instant timestamp;

    @Column(name = "reading_type", length = 40)
    private String readingType;

    @Column(name = "phase", length = 20)
    private String phase;

    @Column(name = "created_at", nullable = false)
    private Instant createdAt;
}
