package com.sems.shared.events;

import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.context.ApplicationEventPublisher;
import org.springframework.stereotype.Component;

/**
 * Unico punto de salida de eventos de dominio del monolito.
 *
 * <p>Reemplaza al {@code KafkaTemplate} del disenio anterior. Publicar aqui no
 * entrega nada de inmediato: los consumidores anotados con
 * {@code @TransactionalEventListener(phase = AFTER_COMMIT)} reciben el evento
 * solo cuando la transaccion del emisor confirma. Eso da la misma garantia que
 * buscabamos con el broker: nadie reacciona a un cambio que despues se revierte.
 *
 * <p>Si el proyecto vuelve a necesitar un broker, este es el unico archivo que
 * cambia: se le anade un adaptador que ademas serialice y publique al topic.
 */
@Slf4j
@Component
@RequiredArgsConstructor
public class DomainEventBus {

    private final ApplicationEventPublisher publisher;

    public void publish(DomainEvents.DomainEvent event) {
        log.debug("Publicando {} para el usuario {}", event.getClass().getSimpleName(), event.userId());
        publisher.publishEvent(event);
    }
}
