package com.sems.devices.infrastructure.persistence.jpa.repositories;

import com.sems.devices.infrastructure.persistence.jpa.entities.DeviceJpaEntity;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

public interface DeviceJpaRepository extends JpaRepository<DeviceJpaEntity, UUID> {

    Optional<DeviceJpaEntity> findByExternalDeviceCode(String externalDeviceCode);

    boolean existsByExternalDeviceCode(String externalDeviceCode);

    List<DeviceJpaEntity> findByUserId(UUID userId);
}
