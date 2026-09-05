package com.sems.energy.interfaces.rest;

import com.sems.energy.application.EnergyCommandService;
import com.sems.energy.application.EnergyQueryService;
import com.sems.energy.interfaces.rest.resources.EnergyResources.*;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.web.bind.annotation.*;

/**
 * Tarifa vigente y consumo por dispositivo.
 *
 * <p>La ruta {@code /api/v1/energy/pricing/current} es la que consulta el
 * frontend para convertir kWh en soles.
 */
@Tag(name = "Energy Pricing", description = "Tarifa electrica y consumo por dispositivo")
@RestController
@RequestMapping("/api/v1/energy")
@RequiredArgsConstructor
public class EnergyPricingController {

    private final EnergyCommandService commands;
    private final EnergyQueryService queries;

    @Operation(summary = "Tarifa electrica vigente")
    @GetMapping("/pricing/current")
    public PricingResponse currentPricing() {
        return PricingResponse.from(commands.currentPrice());
    }

    @Operation(summary = "Consumo actual de un dispositivo")
    @GetMapping("/devices/{deviceId}/consumption/current")
    public ReadingResponse currentConsumption(@PathVariable String deviceId) {
        return ReadingResponse.from(queries.latestByDevice(deviceId));
    }

    @Operation(summary = "Historial de consumo de un dispositivo")
    @GetMapping("/devices/{deviceId}/consumption/history")
    public List<ReadingResponse> consumptionHistory(@PathVariable String deviceId,
                                                    @RequestParam(defaultValue = "50") int limit,
                                                    @RequestParam(defaultValue = "0") int skip) {
        return queries.readingsByDevice(deviceId, limit, skip).stream()
                .map(ReadingResponse::from).toList();
    }
}
