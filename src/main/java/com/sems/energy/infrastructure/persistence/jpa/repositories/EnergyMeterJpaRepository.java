package com.sems.energy.infrastructure.persistence.jpa.repositories;

import com.sems.energy.infrastructure.persistence.jpa.entities.EnergyMeterJpaEntity;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

public interface EnergyMeterJpaRepository extends JpaRepository<EnergyMeterJpaEntity, UUID> {
    Optional<EnergyMeterJpaEntity> findByMeterSerial(String meterSerial);
    List<EnergyMeterJpaEntity> findByUserIdOrderByRegisteredAtDesc(String userId);
}
