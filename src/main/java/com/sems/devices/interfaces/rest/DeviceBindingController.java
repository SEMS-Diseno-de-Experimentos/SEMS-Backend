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

/** Vinculacion de dispositivos con usuarios y hogares. */
@Tag(name = "Device Bindings", description = "Vinculacion y desvinculacion de dispositivos")
@RestController
@RequestMapping("/api/v1/device-management")
@RequiredArgsConstructor
public class DeviceBindingController {

    private final DeviceCommandService commands;
    private final DeviceQueryService queries;

    @Operation(summary = "Vincula un dispositivo a un usuario")
    @PostMapping("/devices/{deviceId}/bindings")
    public ResponseEntity<DeviceBindingResource> bind(@PathVariable UUID deviceId,
                                                      @Valid @RequestBody CreateBindingRequest request) {
        var binding = commands.bind(deviceId, UUID.fromString(request.userId()),
                request.homeId() == null || request.homeId().isBlank()
                        ? null : UUID.fromString(request.homeId()));
        return ResponseEntity.status(HttpStatus.CREATED).body(DeviceBindingResource.from(binding));
    }

    @Operation(summary = "Lista los vinculos de un dispositivo")
    @GetMapping("/devices/{deviceId}/bindings")
    public List<DeviceBindingResource> byDevice(@PathVariable UUID deviceId) {
        return queries.bindingsByDevice(deviceId).stream().map(DeviceBindingResource::from).toList();
    }

    @Operation(summary = "Lista los vinculos de un usuario")
    @GetMapping("/users/{userId}/bindings")
    public List<DeviceBindingResource> byUser(@PathVariable UUID userId) {
        return queries.bindingsByUser(userId).stream().map(DeviceBindingResource::from).toList();
    }

    @Operation(summary = "Desvincula un dispositivo")
    @PatchMapping("/bindings/{bindingId}/unlink")
    public DeviceBindingResource unlink(@PathVariable UUID bindingId) {
        return DeviceBindingResource.from(commands.unbind(bindingId));
    }
}
