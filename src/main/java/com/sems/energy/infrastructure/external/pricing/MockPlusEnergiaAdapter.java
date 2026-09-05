package com.sems.energy.infrastructure.external.pricing;

import com.sems.energy.domain.model.valueobjects.EnergyPrice;
import com.sems.energy.domain.services.EnergyPricingProvider;
import java.time.Instant;
import java.time.ZoneOffset;
import java.time.format.DateTimeFormatter;
import java.util.Random;
import org.springframework.stereotype.Component;

/**
 * Adaptador simulado del proveedor ficticio Plus Energia.
 *
 * <p>Porta el comportamiento exacto del adaptador en Python: la semilla del
 * generador es la fecha del dia, de modo que el precio es aleatorio entre
 * usuarios pero <b>estable durante toda la jornada</b>. Sin esa semilla la
 * tarifa cambiaria en cada peticion y los costes mostrados al usuario bailarian.
 */
@Component
public class MockPlusEnergiaAdapter implements EnergyPricingProvider {

    private static final DateTimeFormatter DAY_KEY =
            DateTimeFormatter.ofPattern("yyyyMMdd").withZone(ZoneOffset.UTC);

    @Override
    public EnergyPrice currentPrice() {
        Instant now = Instant.now();
        Random rng = new Random(DAY_KEY.format(now).hashCode());
        double price = Math.round((0.68 + rng.nextDouble() * (0.92 - 0.68)) * 100.0) / 100.0;
        return new EnergyPrice("Plus Energia", price, "PEN", now);
    }
}
