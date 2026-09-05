package com.sems.payments.domain.model.valueobjects;

import com.sems.shared.errors.AppException;

/**
 * Importe con su moneda.
 *
 * <p>Mantenerlos juntos evita el error clasico de pasear un numero suelto sin
 * saber si son soles, dolares o euros. La moneda se normaliza a minusculas
 * porque es lo que espera Stripe.
 */
public record Money(double amount, String currency) {

    public Money {
        if (amount <= 0) {
            throw AppException.validation("invalid amount");
        }
        currency = currency == null ? "" : currency.trim().toLowerCase();
        if (currency.isEmpty()) {
            throw AppException.validation("invalid currency");
        }
    }

    /** Stripe cobra en la unidad minima: centimos, no soles. */
    public long toMinorUnits() {
        return Math.round(amount * 100);
    }
}
