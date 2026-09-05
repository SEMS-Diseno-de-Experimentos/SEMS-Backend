package com.sems.devices.infrastructure.persistence.jpa.adapters;

import com.sems.devices.domain.model.entities.DeviceEvent;
import com.sems.devices.domain.repositories.DeviceEventRepository;
import com.sems.devices.infrastructure.persistence.jpa.repositories.DeviceEventJpaRepository;
import java.util.List;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Repository;

@Repository
@RequiredArgsConstructor
public class DeviceEventRepositoryAdapter implements DeviceEventRepository {

    private final DeviceEventJpaRepository jpa;

    @Override
    public DeviceEvent save(DeviceEvent event) {
        return DeviceMapper.toDomain(jpa.save(DeviceMapper.toEntity(event)));
    }

    @Override
    public List<DeviceEvent> findByDeviceId(UUID deviceId) {
        return jpa.findByDeviceIdOrderByOccurredAtDesc(deviceId).stream()
                .map(DeviceMapper::toDomain).toList();
    }
}
