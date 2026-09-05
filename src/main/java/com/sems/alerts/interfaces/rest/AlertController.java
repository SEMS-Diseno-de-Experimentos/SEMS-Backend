package com.sems.alerts.interfaces.rest;

import com.sems.alerts.application.AlertCommandService;
import com.sems.alerts.application.AlertQueryService;
import com.sems.alerts.interfaces.rest.resources.AlertResources.*;
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
 * API REST del bounded context de alertas.
 *
 * <p>Rutas identicas a las del microservicio en Go bajo {@code /api/v1}.
 */
@Tag(name = "Alerts", description = "Avisos, umbrales y preferencias de notificacion")
@RestController
@RequestMapping("/api/v1")
@RequiredArgsConstructor
public class AlertController {

    private final AlertCommandService commands;
    private final AlertQueryService queries;

    // ---------------------------------------------------------------- alertas

    @Operation(summary = "Crea una alerta")
    @PostMapping("/alerts")
    public ResponseEntity<AlertResponse> create(@Valid @RequestBody CreateAlertRequest r) {
        var alert = commands.createAlert(UUID.fromString(r.userId()), UUID.fromString(r.deviceId()),
                optionalUuid(r.thresholdId()), optionalUuid(r.inactivityRuleId()),
                r.alertType(), r.title(), r.message(), r.severity(), r.status(), r.triggeredAt());
        return ResponseEntity.status(HttpStatus.CREATED).body(AlertResponse.from(alert));
    }

    @Operation(summary = "Lista todas las alertas")
    @GetMapping("/alerts")
    public List<AlertResponse> all() {
        return queries.allAlerts().stream().map(AlertResponse::from).toList();
    }

    @Operation(summary = "Obtiene una alerta por su identificador")
    @GetMapping("/alerts/{id}")
    public AlertResponse byId(@PathVariable UUID id) {
        return AlertResponse.from(queries.alertById(id));
    }

    @Operation(summary = "Cambia el estado de una alerta")
    @PatchMapping("/alerts/{id}/status")
    public AlertResponse updateStatus(@PathVariable UUID id,
                                      @Valid @RequestBody UpdateAlertStatusRequest r) {
        return AlertResponse.from(commands.updateStatus(id, r.status(), r.resolvedAt()));
    }

    @Operation(summary = "Alertas de un usuario")
    @GetMapping("/users/{userId}/alerts")
    public List<AlertResponse> byUser(@PathVariable UUID userId) {
        return queries.alertsByUser(userId).stream().map(AlertResponse::from).toList();
    }

    // --------------------------------------------------------------- umbrales

    @Operation(summary = "Crea un umbral de consumo")
    @PostMapping("/thresholds")
    public ResponseEntity<ThresholdResponse> createThreshold(
            @Valid @RequestBody CreateThresholdRequest r) {
        var threshold = commands.createThreshold(UUID.fromString(r.userId()),
                UUID.fromString(r.deviceId()), r.thresholdName(), r.metric(), r.operator(),
                r.thresholdValue(), r.active());
        return ResponseEntity.status(HttpStatus.CREATED).body(ThresholdResponse.from(threshold));
    }

    @Operation(summary = "Umbrales de un usuario")
    @GetMapping("/users/{userId}/thresholds")
    public List<ThresholdResponse> thresholdsByUser(@PathVariable UUID userId) {
        return queries.thresholdsByUser(userId).stream().map(ThresholdResponse::from).toList();
    }

    // ---------------------------------------------------- reglas de inactividad

    @Operation(summary = "Crea una regla de inactividad")
    @PostMapping("/inactivity-rules")
    public ResponseEntity<InactivityRuleResponse> createRule(
            @Valid @RequestBody CreateInactivityRuleRequest r) {
        var rule = commands.createInactivityRule(UUID.fromString(r.userId()),
                UUID.fromString(r.deviceId()), r.ruleName(), r.maxInactiveMinutes(), r.active());
        return ResponseEntity.status(HttpStatus.CREATED).body(InactivityRuleResponse.from(rule));
    }

    @Operation(summary = "Reglas de inactividad de un usuario")
    @GetMapping("/users/{userId}/inactivity-rules")
    public List<InactivityRuleResponse> rulesByUser(@PathVariable UUID userId) {
        return queries.rulesByUser(userId).stream().map(InactivityRuleResponse::from).toList();
    }

    // ---------------------------------------------- preferencias de notificacion

    @Operation(summary = "Guarda una preferencia de notificacion")
    @PostMapping("/notification-preferences")
    public ResponseEntity<PreferenceResponse> createPreference(
            @Valid @RequestBody CreatePreferenceRequest r) {
        var preference = commands.createPreference(UUID.fromString(r.userId()), r.channel(),
                r.enabled(), r.minSeverity(), r.quietHoursStart(), r.quietHoursEnd());
        return ResponseEntity.status(HttpStatus.CREATED).body(PreferenceResponse.from(preference));
    }

    @Operation(summary = "Preferencias de notificacion de un usuario")
    @GetMapping("/users/{userId}/notification-preferences")
    public List<PreferenceResponse> preferencesByUser(@PathVariable UUID userId) {
        return queries.preferencesByUser(userId).stream().map(PreferenceResponse::from).toList();
    }

    private static UUID optionalUuid(String value) {
        if (value == null || value.isBlank()) {
            return null;
        }
        return UUID.fromString(value);
    }
}
