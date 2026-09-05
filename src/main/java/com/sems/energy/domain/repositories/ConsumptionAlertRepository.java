package com.sems.energy.domain.repositories;

import com.sems.energy.domain.model.entities.ConsumptionAlert;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

public interface ConsumptionAlertRepository {
    ConsumptionAlert save(ConsumptionAlert alert);
    Optional<ConsumptionAlert> findById(UUID id);
    List<ConsumptionAlert> findByUserId(String userId);
    List<ConsumptionAlert> findUnreadByUserId(String userId);
}
