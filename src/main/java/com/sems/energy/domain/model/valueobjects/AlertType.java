package com.sems.energy.domain.model.valueobjects;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonValue;
import com.sems.shared.errors.AppException;

/** Motivo por el que se genero una alerta de consumo. */
public enum AlertType {
    HIGH_CONSUMPTION("high_consumption"),
    ANOMALY_DETECTED("anomaly_detected"),
    DEVICE_ALWAYS_ON("device_always_on"),
    THRESHOLD_EXCEEDED("threshold_exceeded"),
    UNUSUAL_PATTERN("unusual_pattern");

    private final String wire;

    AlertType(String wire) {
        this.wire = wire;
    }

    @JsonValue
    public String wire() {
        return wire;
    }

    @JsonCreator
    public static AlertType of(String value) {
        if (value == null) {
            throw AppException.validation("invalid alert type");
        }
        for (AlertType type : values()) {
            if (type.wire.equalsIgnoreCase(value) || type.name().equalsIgnoreCase(value)) {
                return type;
            }
        }
        throw AppException.validation("invalid alert type");
    }
}
