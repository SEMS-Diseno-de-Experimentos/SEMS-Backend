package com.sems.energy.infrastructure.persistence.jpa.repositories;

import com.sems.energy.infrastructure.persistence.jpa.entities.DeviceConsumptionJpaEntity;
import java.util.List;
import java.util.UUID;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;

public interface DeviceConsumptionJpaRepository extends JpaRepository<DeviceConsumptionJpaEntity, UUID> {
    List<DeviceConsumptionJpaEntity> findByUserIdOrderByPeriodEndDesc(String userId);
    List<DeviceConsumptionJpaEntity> findByDeviceIdOrderByPeriodEndDesc(String deviceId);
    List<DeviceConsumptionJpaEntity> findByUserIdOrderByTotalKwhDesc(String userId, Pageable pageable);
}
