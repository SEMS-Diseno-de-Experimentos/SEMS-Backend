package com.sems.energy.domain.repositories;

import com.sems.energy.domain.model.entities.DeviceConsumption;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

public interface DeviceConsumptionRepository {
    DeviceConsumption save(DeviceConsumption consumption);
    Optional<DeviceConsumption> findById(UUID id);
    List<DeviceConsumption> findByUserId(String userId);
    List<DeviceConsumption> findByDeviceId(String deviceId);
    /** Los mayores consumidores del usuario, de mayor a menor. */
    List<DeviceConsumption> findTopByUserId(String userId, int limit);
}
