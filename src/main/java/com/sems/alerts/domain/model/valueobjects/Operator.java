package com.sems.alerts.domain.model.valueobjects;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonValue;
import com.sems.shared.errors.AppException;

/**
 * Comparador de un umbral.
 *
 * <p>Se serializa con el simbolo ({@code ">"}, {@code ">="}...), igual que en el
 * servicio original, porque es lo que el usuario elige en la interfaz.
 */
public enum Operator {
    GREATER_THAN(">"),
    GREATER_THAN_OR_EQUAL(">="),
    LESS_THAN("<"),
    LESS_THAN_OR_EQUAL("<="),
    EQUAL("==");

    private final String symbol;

    Operator(String symbol) {
        this.symbol = symbol;
    }

    @JsonValue
    public String symbol() {
        return symbol;
    }

    @JsonCreator
    public static Operator of(String value) {
        if (value == null) {
            throw AppException.validation("unsupported operator");
        }
        String trimmed = value.trim();
        for (Operator operator : values()) {
            if (operator.symbol.equals(trimmed) || operator.name().equalsIgnoreCase(trimmed)) {
                return operator;
            }
        }
        throw AppException.validation("unsupported operator: " + value);
    }

    /** Aplica la comparacion. Es el corazon de la evaluacion de umbrales. */
    public boolean test(double value, double threshold) {
        return switch (this) {
            case GREATER_THAN -> value > threshold;
            case GREATER_THAN_OR_EQUAL -> value >= threshold;
            case LESS_THAN -> value < threshold;
            case LESS_THAN_OR_EQUAL -> value <= threshold;
            case EQUAL -> value == threshold;
        };
    }
}
