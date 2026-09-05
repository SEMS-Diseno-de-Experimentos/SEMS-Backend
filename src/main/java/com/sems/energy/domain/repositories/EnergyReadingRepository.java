package com.sems.energy.domain.repositories;

import com.sems.energy.domain.model.entities.EnergyReading;
import java.time.Instant;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

public interface EnergyReadingRepository {
    EnergyReading save(EnergyReading reading);
    Optional<EnergyReading> findById(UUID id);
    List<EnergyReading> findByUserId(String userId, int limit);
    List<EnergyReading> findByDeviceId(String deviceId, int limit, int skip);
    List<EnergyReading> findByRange(String userId, Instant from, Instant to);
    Optional<EnergyReading> findLatestByMeter(String meterId);
    Optional<EnergyReading> findLatestByDevice(String deviceId);
}
