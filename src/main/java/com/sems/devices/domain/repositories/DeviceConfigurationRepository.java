package com.sems.devices.domain.repositories;

import com.sems.devices.domain.model.entities.DeviceConfiguration;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

public interface DeviceConfigurationRepository {

    DeviceConfiguration save(DeviceConfiguration configuration);

    Optional<DeviceConfiguration> findById(UUID configurationId);

    List<DeviceConfiguration> findByDeviceId(UUID deviceId);

    Optional<DeviceConfiguration> findByDeviceIdAndKey(UUID deviceId, String configKey);
}
