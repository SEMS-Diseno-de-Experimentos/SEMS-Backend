package com.sems.devices.infrastructure.persistence.jpa.entities;

import jakarta.persistence.*;
import java.time.Instant;
import java.util.UUID;
import lombok.*;

@Entity
@Table(name = "dm_device_events",
        indexes = @Index(name = "idx_dm_events_device", columnList = "device_id"))
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
public class DeviceEventJpaEntity {

    @Id
    @Column(name = "event_id", nullable = false, updatable = false)
    private UUID eventId;

    @Column(name = "device_id", nullable = false)
    private UUID deviceId;

    @Column(name = "event_type", nullable = false, length = 40)
    private String eventType;

    @Column(name = "description", columnDefinition = "text")
    private String description;

    @Column(name = "occurred_at", nullable = false)
    private Instant occurredAt;
}
