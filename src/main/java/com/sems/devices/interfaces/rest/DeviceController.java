package com.sems.devices.interfaces.rest;

import com.sems.devices.application.DeviceCommandService;
import com.sems.devices.application.DeviceQueryService;
import com.sems.devices.interfaces.rest.resources.DeviceResources.*;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import java.util.List;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

/**
 * API REST del bounded context de dispositivos.
 *
 * <p>Las rutas son identicas a las del microservicio original bajo
 * {@code /api/v1/device-management}. Mantenerlas iguales es lo que permite
 * cambiar el backend sin tocar el frontend.
 */
@Tag(name = "Device Management", description = "Registro, vinculacion y configuracion de dispositivos")
@RestController
@RequestMapping("/api/v1/device-management")
@RequiredArgsConstructor
public class DeviceController {

    private final DeviceCommandService commands;
    private final DeviceQueryService queries;

    @Operation(summary = "Registra un dispositivo nuevo")
    @PostMapping("/devices")
    public ResponseEntity<DeviceResource> create(@Valid @RequestBody CreateDeviceRequest request) {
        var device = commands.register(
                request.externalDeviceCode(), UUID.fromString(request.userId()),
                request.deviceName(), request.deviceType(),
                request.brand(), request.model(), request.connectionProtocol());
        return ResponseEntity.status(HttpStatus.CREATED).body(DeviceResource.from(device));
    }

    @Operation(summary = "Lista todos los dispositivos")
    @GetMapping("/devices")
    public List<DeviceResource> list() {
        return queries.allDevices().stream().map(DeviceResource::from).toList();
    }

    @Operation(summary = "Obtiene un dispositivo por su identificador")
    @GetMapping("/devices/{deviceId}")
    public DeviceResource byId(@PathVariable UUID deviceId) {
        return DeviceResource.from(queries.deviceById(deviceId));
    }

    @Operation(summary = "Lista los dispositivos de un usuario")
    @GetMapping("/users/{userId}/devices")
    public List<DeviceResource> byUser(@PathVariable UUID userId) {
        return queries.devicesByUser(userId).stream().map(DeviceResource::from).toList();
    }

    @Operation(summary = "Actualiza los datos editables de un dispositivo")
    @PutMapping("/devices/{deviceId}")
    public DeviceResource update(@PathVariable UUID deviceId,
                                 @Valid @RequestBody UpdateDeviceRequest request) {
        return DeviceResource.from(commands.update(deviceId, request.deviceName(),
                request.deviceType(), request.brand(), request.model(), request.connectionProtocol()));
    }

    @Operation(summary = "Cambia el estado de un dispositivo")
    @PatchMapping("/devices/{deviceId}/status")
    public DeviceResource changeStatus(@PathVariable UUID deviceId,
                                       @Valid @RequestBody UpdateDeviceStatusRequest request) {
        return DeviceResource.from(commands.changeStatus(deviceId, request.status()));
    }

    @Operation(summary = "Elimina un dispositivo (borrado logico)")
    @DeleteMapping("/devices/{deviceId}")
    public ResponseEntity<Void> remove(@PathVariable UUID deviceId) {
        commands.remove(deviceId);
        return ResponseEntity.noContent().build();
    }
}
