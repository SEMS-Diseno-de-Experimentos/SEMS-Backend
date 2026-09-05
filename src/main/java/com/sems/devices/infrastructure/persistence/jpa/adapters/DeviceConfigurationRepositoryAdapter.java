package com.sems.devices.infrastructure.persistence.jpa.adapters;

import com.sems.devices.domain.model.entities.DeviceConfiguration;
import com.sems.devices.domain.repositories.DeviceConfigurationRepository;
import com.sems.devices.infrastructure.persistence.jpa.repositories.DeviceConfigurationJpaRepository;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Repository;

@Repository
@RequiredArgsConstructor
public class DeviceConfigurationRepositoryAdapter implements DeviceConfigurationRepository {

    private final DeviceConfigurationJpaRepository jpa;

    @Override
    public DeviceConfiguration save(DeviceConfiguration configuration) {
        return DeviceMapper.toDomain(jpa.save(DeviceMapper.toEntity(configuration)));
    }

    @Override
    public Optional<DeviceConfiguration> findById(UUID configurationId) {
        return jpa.findById(configurationId).map(DeviceMapper::toDomain);
    }

    @Override
    public List<DeviceConfiguration> findByDeviceId(UUID deviceId) {
        return jpa.findByDeviceId(deviceId).stream().map(DeviceMapper::toDomain).toList();
    }

    @Override
    public Optional<DeviceConfiguration> findByDeviceIdAndKey(UUID deviceId, String configKey) {
        return jpa.findByDeviceIdAndConfigKey(deviceId, configKey).map(DeviceMapper::toDomain);
    }
}
