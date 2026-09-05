package com.sems.payments.domain.model.entities;

import java.time.Instant;
import java.util.UUID;
import lombok.Getter;

/**
 * Medio de pago guardado del usuario.
 *
 * <p>Solo se almacenan los datos que Stripe devuelve para mostrar la tarjeta:
 * marca, ultimos cuatro digitos y vencimiento. <b>El numero completo y el CVV
 * nunca tocan esta base de datos.</b>
 */
@Getter
public class PaymentMethod {

    private final UUID paymentMethodId;
    private final UUID userId;
    private final String type;
    private final String brand;
    private final String last4;
    private final int expMonth;
    private final int expYear;
    private final String stripePaymentMethodId;
    private boolean defaultMethod;
    private final Instant createdAt;

    public PaymentMethod(UUID paymentMethodId, UUID userId, String type, String brand, String last4,
                         int expMonth, int expYear, String stripePaymentMethodId,
                         boolean defaultMethod, Instant createdAt) {
        this.paymentMethodId = paymentMethodId;
        this.userId = userId;
        this.type = type;
        this.brand = brand;
        this.last4 = last4;
        this.expMonth = expMonth;
        this.expYear = expYear;
        this.stripePaymentMethodId = stripePaymentMethodId;
        this.defaultMethod = defaultMethod;
        this.createdAt = createdAt;
    }

    public static PaymentMethod create(UUID userId, String type, String brand, String last4,
                                       int expMonth, int expYear, String stripePaymentMethodId,
                                       boolean isDefault) {
        return new PaymentMethod(UUID.randomUUID(), userId, type, brand, last4, expMonth, expYear,
                stripePaymentMethodId, isDefault, Instant.now());
    }

    public void markDefault() {
        this.defaultMethod = true;
    }

    public void removeDefault() {
        this.defaultMethod = false;
    }
}
