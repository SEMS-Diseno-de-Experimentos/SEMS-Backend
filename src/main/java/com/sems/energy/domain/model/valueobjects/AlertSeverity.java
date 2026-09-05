package com.sems.energy.domain.model.valueobjects;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonValue;
import com.sems.shared.errors.AppException;

/** Urgencia de la alerta, de menor a mayor. */
public enum AlertSeverity {
    LOW("low"),
    MEDIUM("medium"),
    HIGH("high"),
    CRITICAL("critical");

    private final String wire;

    AlertSeverity(String wire) {
        this.wire = wire;
    }

    @JsonValue
    public String wire() {
        return wire;
    }

    @JsonCreator
    public static AlertSeverity of(String value) {
        if (value == null) {
            throw AppException.validation("invalid alert severity");
        }
        for (AlertSeverity severity : values()) {
            if (severity.wire.equalsIgnoreCase(value) || severity.name().equalsIgnoreCase(value)) {
                return severity;
            }
        }
        throw AppException.validation("invalid alert severity");
    }
}
