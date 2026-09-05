package com.sems.devices.domain.repositories;

import com.sems.devices.domain.model.entities.DeviceBinding;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

public interface DeviceBindingRepository {

    DeviceBinding save(DeviceBinding binding);

    Optional<DeviceBinding> findById(UUID bindingId);

    List<DeviceBinding> findByDeviceId(UUID deviceId);

    List<DeviceBinding> findByUserId(UUID userId);

    /** Un dispositivo no puede tener dos vinculos activos a la vez. */
    Optional<DeviceBinding> findActiveByDeviceId(UUID deviceId);
}
