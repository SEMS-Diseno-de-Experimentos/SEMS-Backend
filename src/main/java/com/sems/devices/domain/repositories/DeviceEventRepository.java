package com.sems.devices.domain.repositories;

import com.sems.devices.domain.model.entities.DeviceEvent;
import java.util.List;
import java.util.UUID;

public interface DeviceEventRepository {

    DeviceEvent save(DeviceEvent event);

    /** Ordenados del mas reciente al mas antiguo. */
    List<DeviceEvent> findByDeviceId(UUID deviceId);
}
