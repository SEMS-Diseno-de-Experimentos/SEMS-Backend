package com.sems.devices.domain.model.valueobjects;

import com.sems.shared.errors.AppException;

/** Protocolo por el que el dispositivo se comunica. */
public enum ConnectionProtocol {
    WIFI,
    BLUETOOTH;

    public static ConnectionProtocol of(String value) {
        if (value == null) {
            throw AppException.validation("connection_protocol is invalid");
        }
        try {
            return ConnectionProtocol.valueOf(value.trim().toUpperCase());
        } catch (IllegalArgumentException e) {
            throw AppException.validation("connection_protocol is invalid");
        }
    }
}
