package com.sems.energy.infrastructure.persistence.jpa.repositories;

import com.sems.energy.infrastructure.persistence.jpa.entities.ConsumptionAlertJpaEntity;
import java.util.List;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

public interface ConsumptionAlertJpaRepository extends JpaRepository<ConsumptionAlertJpaEntity, UUID> {
    List<ConsumptionAlertJpaEntity> findByUserIdOrderByCreatedAtDesc(String userId);
    List<ConsumptionAlertJpaEntity> findByUserIdAndReadFalseOrderByCreatedAtDesc(String userId);
}
