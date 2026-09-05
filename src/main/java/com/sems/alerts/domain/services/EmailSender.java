package com.sems.alerts.domain.services;

/**
 * Puerto de envio de correo.
 *
 * <p>El dominio no sabe si detras hay Gmail, un servicio transaccional o un
 * doble de pruebas. Eso permite probar el flujo de notificaciones sin enviar
 * correos de verdad.
 */
public interface EmailSender {

    /**
     * @throws RuntimeException si el envio falla; quien llama decide si reintenta
     */
    void send(String to, String subject, String body);
}
