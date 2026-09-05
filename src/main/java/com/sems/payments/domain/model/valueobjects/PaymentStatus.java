package com.sems.payments.domain.model.valueobjects;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonValue;
import com.sems.shared.errors.AppException;

/**
 * Ciclo de vida de un pago.
 *
 * <p>El camino normal es pending, processing, processed. Fallido y cancelado son
 * estados finales. Se serializan en minusculas, igual que en el servicio en Go.
 */
public enum PaymentStatus {
    PENDING("pending"),
    PROCESSING("processing"),
    PROCESSED("processed"),
    FAILED("failed"),
    CANCELLED("cancelled");

    private final String wire;

    PaymentStatus(String wire) {
        this.wire = wire;
    }

    @JsonValue
    public String wire() {
        return wire;
    }

    @JsonCreator
    public static PaymentStatus of(String value) {
        if (value == null) {
            throw AppException.validation("invalid payment status");
        }
        for (PaymentStatus status : values()) {
            if (status.wire.equalsIgnoreCase(value) || status.name().equalsIgnoreCase(value)) {
                return status;
            }
        }
        throw AppException.validation("invalid payment status");
    }
}
