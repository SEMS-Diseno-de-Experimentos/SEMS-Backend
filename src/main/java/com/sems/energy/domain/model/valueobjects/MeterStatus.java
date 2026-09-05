package com.sems.energy.domain.model.valueobjects;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonValue;
import com.sems.shared.errors.AppException;

/**
 * Estado del medidor.
 *
 * <p>El servicio en Python serializaba estos valores en minusculas
 * ({@code "active"}, {@code "inactive"}...). {@code @JsonValue} conserva ese
 * formato exacto para no romper al frontend.
 */
public enum MeterStatus {
    ACTIVE("active"),
    INACTIVE("inactive"),
    MAINTENANCE("maintenance"),
    ERROR("error");

    private final String wire;

    MeterStatus(String wire) {
        this.wire = wire;
    }

    @JsonValue
    public String wire() {
        return wire;
    }

    @JsonCreator
    public static MeterStatus of(String value) {
        if (value == null) {
            throw AppException.validation("invalid meter status");
        }
        for (MeterStatus status : values()) {
            if (status.wire.equalsIgnoreCase(value) || status.name().equalsIgnoreCase(value)) {
                return status;
            }
        }
        throw AppException.validation("invalid meter status");
    }
}
