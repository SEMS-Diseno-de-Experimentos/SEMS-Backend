package com.sems.payments.domain.model.entities;

import java.time.Instant;
import java.util.UUID;
import lombok.Getter;

/**
 * Evento recibido del proveedor de pagos.
 *
 * <p>Se guarda el cuerpo original para auditoria y, sobre todo, para detectar
 * reenvios: Stripe reintenta los webhooks, asi que sin este registro un mismo
 * cobro podria contabilizarse dos veces.
 */
@Getter
public class PaymentWebhookEvent {

    public static final String PROVIDER_STRIPE = "stripe";

    private final UUID eventId;
    private final String provider;
    private final String providerEventId;
    private final String eventType;
    private final String payload;
    private boolean processed;
    private final Instant receivedAt;
    private Instant processedAt;

    public PaymentWebhookEvent(UUID eventId, String provider, String providerEventId,
                               String eventType, String payload, boolean processed,
                               Instant receivedAt, Instant processedAt) {
        this.eventId = eventId;
        this.provider = provider;
        this.providerEventId = providerEventId;
        this.eventType = eventType;
        this.payload = payload;
        this.processed = processed;
        this.receivedAt = receivedAt;
        this.processedAt = processedAt;
    }

    public static PaymentWebhookEvent received(String provider, String providerEventId,
                                               String eventType, String payload) {
        return new PaymentWebhookEvent(UUID.randomUUID(), provider, providerEventId, eventType,
                payload, false, Instant.now(), null);
    }

    public void markProcessed() {
        this.processed = true;
        this.processedAt = Instant.now();
    }
}
