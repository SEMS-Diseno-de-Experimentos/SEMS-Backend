package com.sems.payments.domain.repositories;

import com.sems.payments.domain.model.entities.*;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

/** Puertos de salida del modulo de pagos. */
public final class PaymentRepositories {

    private PaymentRepositories() {
    }

    public interface PaymentRepository {
        Payment save(Payment payment);
        Optional<Payment> findById(UUID paymentId);
        List<Payment> findByUserId(UUID userId);
        List<Payment> findBySubscriptionId(UUID subscriptionId);
        Optional<Payment> findByStripePaymentIntentId(String stripePaymentIntentId);
    }

    public interface PaymentMethodRepository {
        PaymentMethod save(PaymentMethod method);
        Optional<PaymentMethod> findById(UUID paymentMethodId);
        List<PaymentMethod> findByUserId(UUID userId);
        Optional<PaymentMethod> findDefaultByUserId(UUID userId);
        void deleteById(UUID paymentMethodId);
    }

    public interface InvoiceRepository {
        Invoice save(Invoice invoice);
        Optional<Invoice> findById(UUID invoiceId);
        Optional<Invoice> findByPaymentId(UUID paymentId);
    }

    public interface WebhookEventRepository {
        PaymentWebhookEvent save(PaymentWebhookEvent event);
        /** Sirve para descartar reenvios del mismo evento. */
        Optional<PaymentWebhookEvent> findByProviderEventId(String providerEventId);
    }
}
