package com.sems.shared.errors;

/**
 * Cuerpo de error devuelto por la API.
 *
 * <p>Forma exacta que ya esperaba el frontend cuando el backend eran
 * microservicios en Go: no cambia ni el nombre ni el tipo de los campos.
 */
public record ErrorResponse(String code, String message) {
}
