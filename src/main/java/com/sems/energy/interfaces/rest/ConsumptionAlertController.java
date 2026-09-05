package com.sems.energy.interfaces.rest;

import com.sems.energy.application.EnergyCommandService;
import com.sems.energy.application.EnergyQueryService;
import com.sems.energy.interfaces.rest.resources.EnergyResources.*;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import java.util.List;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import org.springframework.web.bind.annotation.*;

/** Alertas generadas por el propio modulo de monitoreo. */
@Tag(name = "Consumption Alerts", description = "Avisos por consumo fuera de lo esperado")
@RestController
@RequestMapping("/api/v1/consumption-alerts")
@RequiredArgsConstructor
public class ConsumptionAlertController {

    private final EnergyCommandService commands;
    private final EnergyQueryService queries;

    @Operation(summary = "Alertas de un usuario")
    @GetMapping("/user/{userId}")
    public List<AlertResponse> byUser(@PathVariable String userId) {
        return queries.alertsByUser(userId).stream().map(AlertResponse::from).toList();
    }

    @Operation(summary = "Alertas sin leer de un usuario")
    @GetMapping("/user/{userId}/unread")
    public List<AlertResponse> unreadByUser(@PathVariable String userId) {
        return queries.unreadAlertsByUser(userId).stream().map(AlertResponse::from).toList();
    }

    @Operation(summary = "Obtiene una alerta por su identificador")
    @GetMapping("/{alertId}")
    public AlertResponse byId(@PathVariable UUID alertId) {
        return AlertResponse.from(queries.alertById(alertId));
    }

    @Operation(summary = "Marca una alerta como leida")
    @PatchMapping("/{alertId}/read")
    public AlertResponse markRead(@PathVariable UUID alertId) {
        return AlertResponse.from(commands.markAlertRead(alertId));
    }

    @Operation(summary = "Da por resuelta una alerta")
    @PatchMapping("/{alertId}/resolve")
    public AlertResponse resolve(@PathVariable UUID alertId) {
        return AlertResponse.from(commands.resolveAlert(alertId));
    }
}
