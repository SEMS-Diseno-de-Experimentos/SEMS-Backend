package com.sems.devices.infrastructure.persistence.jpa.repositories;

import com.sems.devices.domain.model.valueobjects.BindingStatus;
import com.sems.devices.infrastructure.persistence.jpa.entities.DeviceBindingJpaEntity;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

public interface DeviceBindingJpaRepository extends JpaRepository<DeviceBindingJpaEntity, UUID> {

    List<DeviceBindingJpaEntity> findByDeviceId(UUID deviceId);

    List<DeviceBindingJpaEntity> findByUserId(UUID userId);

    Optional<DeviceBindingJpaEntity> findFirstByDeviceIdAndBindingStatus(UUID deviceId, BindingStatus status);
}
