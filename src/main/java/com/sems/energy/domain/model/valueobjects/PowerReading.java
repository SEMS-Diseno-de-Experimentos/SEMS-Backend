package com.sems.energy.domain.model.valueobjects;

import com.sems.shared.errors.AppException;

/**
 * Medicion electrica instantanea. Value object inmutable.
 *
 * <p>Las validaciones son las mismas que hacia {@code __post_init__} en Python:
 * ninguna magnitud puede ser negativa y la frecuencia tiene que estar en el
 * rango fisico de una red electrica.
 */
public record PowerReading(double powerWatts, double voltage, double current,
                           double frequency, double energyKwh) {

    public PowerReading {
        if (powerWatts < 0) {
            throw AppException.validation("Power watts cannot be negative.");
        }
        if (voltage < 0) {
            throw AppException.validation("Voltage cannot be negative.");
        }
        if (current < 0) {
            throw AppException.validation("Current cannot be negative.");
        }
        if (frequency < 45.0 || frequency > 65.0) {
            throw AppException.validation("Frequency must be between 45 and 65 Hz.");
        }
    }

    /** Potencia aparente en voltiamperios. */
    public double apparentPowerVa() {
        return voltage * current;
    }

    /** Factor de potencia, acotado a 1. */
    public double powerFactor() {
        double apparent = apparentPowerVa();
        if (apparent == 0) {
            return 1.0;
        }
        return Math.min(powerWatts / apparent, 1.0);
    }
}
