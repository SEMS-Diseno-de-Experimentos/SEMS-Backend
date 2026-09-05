package com.sems.devices.infrastructure.persistence.jpa.repositories;

import com.sems.devices.infrastructure.persistence.jpa.entities.DeviceEventJpaEntity;
import java.util.List;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

public interface DeviceEventJpaRepository extends JpaRepository<DeviceEventJpaEntity, UUID> {

    List<DeviceEventJpaEntity> findByDeviceIdOrderByOccurredAtDesc(UUID deviceId);
}
