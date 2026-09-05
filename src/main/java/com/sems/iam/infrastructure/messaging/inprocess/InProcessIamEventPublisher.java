package com.sems.iam.infrastructure.messaging.inprocess;

import com.sems.iam.application.internal.outboundservices.IamEventPublisher;
import com.sems.shared.events.DomainEventBus;
import com.sems.shared.events.DomainEvents;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;

/**
 * Adaptador de salida del bounded context IAM.
 *
 * <p>Sustituye a {@code KafkaIamEventPublisher}. El puerto
 * {@link IamEventPublisher} no cambia: la capa de aplicacion sigue sin saber
 * como se entregan los eventos, que es justamente lo que permitio cambiar de
 * Kafka a entrega en proceso sin tocar los command services.
 */
@Slf4j
@Service
@RequiredArgsConstructor
public class InProcessIamEventPublisher implements IamEventPublisher {

    private final DomainEventBus bus;

    @Override
    public void publishUserRegistered(String userId, String emailAddress, String role) {
        parse(userId).ifPresent(id ->
                bus.publish(new DomainEvents.UserRegistered(id, emailAddress, role)));
    }

    @Override
    public void publishUserLoggedIn(String userId, String emailAddress) {
        parse(userId).ifPresent(id ->
                bus.publish(new DomainEvents.UserLoggedIn(id, emailAddress)));
    }

    @Override
    public void publishRoleAssigned(String userId, String role) {
        parse(userId).ifPresent(id ->
                bus.publish(new DomainEvents.RoleAssigned(id, role)));
    }

    @Override
    public void publishVerificationRequested(String userId, String emailAddress, String token) {
        parse(userId).ifPresent(id ->
                bus.publish(new DomainEvents.VerificationRequested(id, emailAddress, token)));
    }

    @Override
    public void publishPasswordResetRequested(String userId, String emailAddress, String token) {
        parse(userId).ifPresent(id ->
                bus.publish(new DomainEvents.PasswordResetRequested(id, emailAddress, token)));
    }

    /**
     * Los identificadores llegan como texto desde la capa de aplicacion. Uno
     * malformado no debe tumbar la operacion de negocio que lo origino: se
     * registra y el evento se descarta.
     */
    private java.util.Optional<UUID> parse(String userId) {
        try {
            return java.util.Optional.of(UUID.fromString(userId));
        } catch (IllegalArgumentException | NullPointerException e) {
            log.warn("Identificador de usuario invalido en evento IAM: {}", userId);
            return java.util.Optional.empty();
        }
    }
}
