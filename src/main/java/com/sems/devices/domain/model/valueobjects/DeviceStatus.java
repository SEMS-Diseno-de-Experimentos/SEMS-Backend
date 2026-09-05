package com.sems.devices.domain.model.valueobjects;

import com.sems.shared.errors.AppException;

/**
 * Estado de un dispositivo.
 *
 * <p>Portado del value object {@code DeviceStatus} en Go. Un enum de Java da la
 * misma garantia que alli se lograba con un tipo string con nombre: el
 * compilador impide pasar cualquier cadena donde se espera un estado.
 */
public enum DeviceStatus {
    ACTIVE,
    INACTIVE,
    DISCONNECTED,
    REMOVED;

    /** Convierte texto no confiable, por ejemplo el cuerpo de una peticion. */
    public static DeviceStatus of(String value) {
        if (value == null) {
            throw AppException.validation("invalid device status");
        }
        try {
            return DeviceStatus.valueOf(value.trim().toUpperCase());
        } catch (IllegalArgumentException e) {
            throw AppException.validation("invalid device status");
        }
    }

    /**
     * Maquina de estados del dispositivo. El orden de las reglas importa:
     * <ol>
     *   <li>REMOVED es terminal: de ahi no se sale.</li>
     *   <li>Cualquier dispositivo vivo puede pasar a REMOVED.</li>
     *   <li>El resto de transiciones solo valen entre estados vivos.</li>
     * </ol>
     */
    public boolean canTransitionTo(DeviceStatus next) {
        if (this == REMOVED) {
            return false;
        }
        if (next == REMOVED) {
            return true;
        }
        return next == ACTIVE || next == INACTIVE || next == DISCONNECTED;
    }
}
