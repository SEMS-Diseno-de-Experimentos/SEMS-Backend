package com.sems.payments.application;

import com.sems.payments.domain.model.entities.PaymentWebhookEvent;
import com.sems.payments.domain.model.valueobjects.PaymentStatus;
import com.sems.payments.domain.repositories.PaymentRepositories.*;
import com.sems.payments.domain.services.PaymentProvider;
import com.sems.payments.domain.services.PaymentStatusMapper;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * Procesa los avisos que envia Stripe.
 *
 * <p>El webhook es la unica fuente fiable sobre si el dinero se movio: la
 * respuesta inmediata al usuario puede perderse si cierra el navegador, pero
 * Stripe reintenta el aviso hasta que respondemos 200.
 */
@Slf4j
@Service
@RequiredArgsConstructor
public class WebhookCommandService {

    private final WebhookEventRepository events;
    private final PaymentRepository payments;
    private final InvoiceRepository invoices;
    private final PaymentProvider provider;
    private final PaymentStatusMapper statusMapper;
    private final PaymentCommandService paymentCommands;

    /**
     * @return true si el evento se proceso, false si era un reenvio ya conocido
     */
    @Transactional
    public boolean handleStripe(String payload, String signature) {
        var event = provider.parseWebhookEvent(payload, signature);

        // Stripe reintenta: un evento ya visto se descarta sin volver a cobrar.
        if (events.findByProviderEventId(event.providerEventId()).isPresent()) {
            log.info("Webhook duplicado descartado: {}", event.providerEventId());
            return false;
        }

        PaymentWebhookEvent stored = events.save(PaymentWebhookEvent.received(
                PaymentWebhookEvent.PROVIDER_STRIPE, event.providerEventId(),
                event.eventType(), event.payload()));

        switch (event.eventType()) {
            case "checkout.session.completed" -> handleCheckoutCompleted(event);
            case "payment_intent.succeeded", "payment_intent.payment_failed",
                 "payment_intent.canceled", "payment_intent.processing" ->
                    handlePaymentIntent(event);
            default -> log.debug("Evento de Stripe sin manejador: {}", event.eventType());
        }

        stored.markProcessed();
        events.save(stored);
        return true;
    }

    /** El usuario completo el pago en la pagina alojada por Stripe. */
    private void handleCheckoutCompleted(PaymentProvider.ProviderWebhookEvent event) {
        UUID userId = parse(event.checkoutUserId());
        if (userId == null) {
            log.warn("checkout.session.completed sin user_id en los metadatos; se ignora");
            return;
        }
        paymentCommands.recordCheckoutPayment(userId, parse(event.checkoutSubscriptionId()),
                event.checkoutAmount() == null ? 0.0 : event.checkoutAmount(),
                event.checkoutCurrency(), event.stripePaymentIntentId());
    }

    /** Cambio de estado de un cobro iniciado desde la aplicacion. */
    private void handlePaymentIntent(PaymentProvider.ProviderWebhookEvent event) {
        payments.findByStripePaymentIntentId(event.stripePaymentIntentId()).ifPresentOrElse(
                payment -> {
                    PaymentStatus status = statusMapper.fromStripe(event.paymentStatus());
                    switch (status) {
                        case PROCESSED -> payment.markProcessed(event.stripePaymentIntentId());
                        case FAILED -> payment.markFailed(event.stripePaymentIntentId());
                        case CANCELLED -> payment.markCancelled(event.stripePaymentIntentId());
                        default -> payment.markProcessing(event.stripePaymentIntentId());
                    }
                    var saved = payments.save(payment);
                    if (saved.isPaid() && invoices.findByPaymentId(saved.getPaymentId()).isEmpty()) {
                        invoices.save(com.sems.payments.domain.model.entities.Invoice
                                .issueFor(saved.getPaymentId(), saved.getAmount(), ""));
                    }
                },
                () -> log.warn("Webhook para un intent desconocido: {}",
                        event.stripePaymentIntentId()));
    }

    private static UUID parse(String value) {
        try {
            return UUID.fromString(value);
        } catch (IllegalArgumentException | NullPointerException e) {
            return null;
        }
    }
}
