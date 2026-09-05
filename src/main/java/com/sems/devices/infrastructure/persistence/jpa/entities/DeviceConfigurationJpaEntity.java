package com.sems.devices.infrastructure.persistence.jpa.entities;

import jakarta.persistence.*;
import java.time.Instant;
import java.util.UUID;
import lombok.*;

@Entity
@Table(name = "dm_device_configurations",
        uniqueConstraints = @UniqueConstraint(name = "uk_dm_config_device_key",
                columnNames = {"device_id", "config_key"}))
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
public class DeviceConfigurationJpaEntity {

    @Id
    @Column(name = "configuration_id", nullable = false, updatable = false)
    private UUID configurationId;

    @Column(name = "device_id", nullable = false)
    private UUID deviceId;

    @Column(name = "config_key", nullable = false, length = 120)
    private String configKey;

    @Column(name = "config_value", columnDefinition = "text")
    private String configValue;

    @Column(name = "updated_at", nullable = false)
    private Instant updatedAt;
}
