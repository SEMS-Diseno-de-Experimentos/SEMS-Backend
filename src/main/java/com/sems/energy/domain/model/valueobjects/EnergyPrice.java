package com.sems.energy.domain.model.valueobjects;

import java.time.Instant;

/** Precio de la electricidad publicado por el proveedor externo. */
public record EnergyPrice(String provider, double pricePerKwh, String currency, Instant timestamp) {
}
