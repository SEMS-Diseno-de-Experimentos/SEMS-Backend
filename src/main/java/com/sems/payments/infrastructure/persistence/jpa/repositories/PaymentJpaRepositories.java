package com.sems.payments.infrastructure.persistence.jpa.repositories;

import com.sems.payments.infrastructure.persistence.jpa.entities.PaymentJpaEntities.*;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

/** Repositorios de Spring Data del modulo de pagos. */
public final class PaymentJpaRepositories {

    private PaymentJpaRepositories() {
    }

    public interface PaymentJpa extends JpaRepository<PaymentRow, UUID> {
        List<PaymentRow> findByUserIdOrderByCreatedAtDesc(UUID userId);
        List<PaymentRow> findBySubscriptionIdOrderByCreatedAtDesc(UUID subscriptionId);
        Optional<PaymentRow> findByStripePaymentIntentId(String stripePaymentIntentId);
    }

    public interface PaymentMethodJpa extends JpaRepository<PaymentMethodRow, UUID> {
        List<PaymentMethodRow> findByUserIdOrderByCreatedAtDesc(UUID userId);
        Optional<PaymentMethodRow> findFirstByUserIdAndDefaultMethodTrue(UUID userId);
    }

    public interface InvoiceJpa extends JpaRepository<InvoiceRow, UUID> {
        Optional<InvoiceRow> findByPaymentId(UUID paymentId);
    }

    public interface WebhookEventJpa extends JpaRepository<WebhookEventRow, UUID> {
        Optional<WebhookEventRow> findByProviderEventId(String providerEventId);
    }
}
