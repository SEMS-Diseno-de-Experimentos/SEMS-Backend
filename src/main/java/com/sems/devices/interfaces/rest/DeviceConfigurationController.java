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

/** Ajustes con nombre asociados a un dispositivo. */
@Tag(name = "Device Configurations", description = "Ajustes de cada dispositivo")
@RestController
@RequestMapping("/api/v1/device-management")
@RequiredArgsConstructor
public class DeviceConfigurationController {

    private final DeviceCommandService commands;
    private final DeviceQueryService queries;

    @Operation(summary = "Crea o actualiza un ajuste del dispositivo")
    @PostMapping("/devices/{deviceId}/configurations")
    public ResponseEntity<DeviceConfigurationResource> upsert(
            @PathVariable UUID deviceId,
            @Valid @RequestBody CreateConfigurationRequest request) {
        var configuration = commands.upsertConfiguration(deviceId, request.configKey(), request.configValue());
        return ResponseEntity.status(HttpStatus.CREATED)
                .body(DeviceConfigurationResource.from(configuration));
    }

    @Operation(summary = "Lista los ajustes de un dispositivo")
    @GetMapping("/devices/{deviceId}/configurations")
    public List<DeviceConfigurationResource> byDevice(@PathVariable UUID deviceId) {
        return queries.configurationsByDevice(deviceId).stream()
                .map(DeviceConfigurationResource::from).toList();
    }

    @Operation(summary = "Actualiza el valor de un ajuste")
    @PutMapping("/configurations/{configurationId}")
    public DeviceConfigurationResource update(@PathVariable UUID configurationId,
                                              @RequestBody UpdateConfigurationRequest request) {
        return DeviceConfigurationResource.from(
                commands.updateConfiguration(configurationId, request.configValue()));
    }
}
