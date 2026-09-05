package com.sems.devices.infrastructure.persistence.jpa.repositories;

import com.sems.devices.infrastructure.persistence.jpa.entities.DeviceConfigurationJpaEntity;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

public interface DeviceConfigurationJpaRepository
        extends JpaRepository<DeviceConfigurationJpaEntity, UUID> {

    List<DeviceConfigurationJpaEntity> findByDeviceId(UUID deviceId);

    Optional<DeviceConfigurationJpaEntity> findByDeviceIdAndConfigKey(UUID deviceId, String configKey);
}
