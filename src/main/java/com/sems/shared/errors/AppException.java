package com.sems.shared.errors;

/**
 * Error de aplicacion comun a todos los modulos.
 *
 * <p>Traduce el {@code AppError} que los servicios en Go compartian entre capas.
 * El dominio lanza esta excepcion y la capa REST la convierte en el mismo JSON
 * que ya consume el frontend: <code>{"code": "...", "message": "..."}</code>.
 *
 * <p>Mantener el contrato de error identico es lo que permite cambiar la
 * implementacion del backend sin tocar el manejo de errores del cliente.
 */
public class AppException extends RuntimeException {

    public enum Code {
        VALIDATION_ERROR,
        NOT_FOUND,
        CONFLICT,
        UNAUTHORIZED,
        INTERNAL_ERROR
    }

    private final Code code;

    public AppException(Code code, String message) {
        super(message);
        this.code = code;
    }

    public Code code() {
        return code;
    }

    public static AppException validation(String message) {
        return new AppException(Code.VALIDATION_ERROR, message);
    }

    public static AppException notFound(String message) {
        return new AppException(Code.NOT_FOUND, message);
    }

    public static AppException conflict(String message) {
        return new AppException(Code.CONFLICT, message);
    }

    public static AppException unauthorized(String message) {
        return new AppException(Code.UNAUTHORIZED, message);
    }

    public static AppException internal(String message) {
        return new AppException(Code.INTERNAL_ERROR, message);
    }
}
