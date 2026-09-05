package com.sems.iam.application.internal.outboundservices;

/** Puerto de salida de eventos del bounded context IAM. */
public interface IamEventPublisher {

    void publishUserRegistered(String userId, String emailAddress, String role);

    void publishUserLoggedIn(String userId, String emailAddress);

    void publishRoleAssigned(String userId, String role);

    /**
     * Pide el envio del codigo de verificacion.
     *
     * <p>El token viaja en el evento porque es la unica vez que existe en claro:
     * en base de datos solo queda su resumen.
     */
    void publishVerificationRequested(String userId, String emailAddress, String token);

    /** Pide el envio del enlace de recuperacion de contrasena. */
    void publishPasswordResetRequested(String userId, String emailAddress, String token);
}
