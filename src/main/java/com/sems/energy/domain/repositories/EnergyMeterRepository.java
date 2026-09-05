package com.sems.energy.domain.repositories;

import com.sems.energy.domain.model.entities.EnergyMeter;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

public interface EnergyMeterRepository {
    EnergyMeter save(EnergyMeter meter);
    Optional<EnergyMeter> findById(UUID id);
    Optional<EnergyMeter> findBySerial(String meterSerial);
    List<EnergyMeter> findByUserId(String userId);
}
