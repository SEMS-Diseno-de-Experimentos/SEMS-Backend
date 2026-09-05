package com.sems.devices.infrastructure.persistence.jpa.adapters;

import com.sems.devices.domain.model.entities.DeviceBinding;
import com.sems.devices.domain.model.valueobjects.BindingStatus;
import com.sems.devices.domain.repositories.DeviceBindingRepository;
import com.sems.devices.infrastructure.persistence.jpa.repositories.DeviceBindingJpaRepository;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Repository;

@Repository
@RequiredArgsConstructor
public class DeviceBindingRepositoryAdapter implements DeviceBindingRepository {

    private final DeviceBindingJpaRepository jpa;

    @Override
    public DeviceBinding save(DeviceBinding binding) {
        return DeviceMapper.toDomain(jpa.save(DeviceMapper.toEntity(binding)));
    }

    @Override
    public Optional<DeviceBinding> findById(UUID bindingId) {
        return jpa.findById(bindingId).map(DeviceMapper::toDomain);
    }

    @Override
    public List<DeviceBinding> findByDeviceId(UUID deviceId) {
        return jpa.findByDeviceId(deviceId).stream().map(DeviceMapper::toDomain).toList();
    }

    @Override
    public List<DeviceBinding> findByUserId(UUID userId) {
        return jpa.findByUserId(userId).stream().map(DeviceMapper::toDomain).toList();
    }

    @Override
    public Optional<DeviceBinding> findActiveByDeviceId(UUID deviceId) {
        return jpa.findFirstByDeviceIdAndBindingStatus(deviceId, BindingStatus.LINKED)
                .map(DeviceMapper::toDomain);
    }
}
