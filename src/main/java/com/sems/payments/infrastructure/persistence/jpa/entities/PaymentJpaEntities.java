package com.sems.payments.infrastructure.persistence.jpa.entities;

import com.sems.payments.domain.model.valueobjects.PaymentStatus;
import jakarta.persistence.*;
import java.time.Instant;
import java.util.UUID;
import lombok.*;

/** Filas de las tablas del modulo de pagos. */
public final class PaymentJpaEntities {

    private PaymentJpaEntities() {
    }

    @Entity
    @Table(name = "pm_payments",
            indexes = {
                    @Index(name = "idx_pm_pay_user", columnList = "user_id"),
                    @Index(name = "idx_pm_pay_sub", columnList = "subscription_id"),
                    @Index(name = "idx_pm_pay_intent", columnList = "stripe_payment_intent_id")
            })
    @Getter @Setter @NoArgsConstructor @AllArgsConstructor
    public static class PaymentRow {
        @Id @Column(name = "payment_id", nullable = false, updatable = false) private UUID paymentId;
        @Column(name = "subscription_id") private UUID subscriptionId;
        @Column(name = "user_id", nullable = false) private UUID userId;
        @Column(name = "payment_method_id") private UUID paymentMethodId;
        @Column(name = "amount", nullable = false) private double amount;
        @Column(name = "currency", length = 10) private String currency;
        @Enumerated(EnumType.STRING)
        @Column(name = "status", nullable = false, length = 20) private PaymentStatus status;
        @Column(name = "payment_method", length = 60) private String paymentMethod;
        @Column(name = "stripe_payment_intent_id", length = 120) private String stripePaymentIntentId;
        @Column(name = "paid_at") private Instant paidAt;
        @Column(name = "created_at", nullable = false) private Instant createdAt;
    }

    @Entity
    @Table(name = "pm_payment_methods",
            indexes = @Index(name = "idx_pm_method_user", columnList = "user_id"))
    @Getter @Setter @NoArgsConstructor @AllArgsConstructor
    public static class PaymentMethodRow {
        @Id @Column(name = "payment_method_id", nullable = false, updatable = false) private UUID paymentMethodId;
        @Column(name = "user_id", nullable = false) private UUID userId;
        @Column(name = "type", length = 40) private String type;
        @Column(name = "brand", length = 40) private String brand;
        @Column(name = "last4", length = 8) private String last4;
        @Column(name = "exp_month") private int expMonth;
        @Column(name = "exp_year") private int expYear;
        @Column(name = "stripe_payment_method_id", length = 120) private String stripePaymentMethodId;
        @Column(name = "is_default", nullable = false) private boolean defaultMethod;
        @Column(name = "created_at", nullable = false) private Instant createdAt;
    }

    @Entity
    @Table(name = "pm_invoices",
            indexes = @Index(name = "idx_pm_invoice_payment", columnList = "payment_id"))
    @Getter @Setter @NoArgsConstructor @AllArgsConstructor
    public static class InvoiceRow {
        @Id @Column(name = "invoice_id", nullable = false, updatable = false) private UUID invoiceId;
        @Column(name = "payment_id", nullable = false) private UUID paymentId;
        @Column(name = "invoice_number", length = 60) private String invoiceNumber;
        @Column(name = "issued_at", nullable = false) private Instant issuedAt;
        @Column(name = "total_amount", nullable = false) private double totalAmount;
        @Column(name = "pdf_url", length = 500) private String pdfUrl;
    }

    /**
     * Evento de webhook.
     *
     * <p>El identificador del proveedor es unico: es lo que impide procesar dos
     * veces el mismo cobro cuando Stripe reintenta la entrega.
     */
    @Entity
    @Table(name = "pm_webhook_events",
            uniqueConstraints = @UniqueConstraint(name = "uk_pm_webhook_provider_event",
                    columnNames = "provider_event_id"))
    @Getter @Setter @NoArgsConstructor @AllArgsConstructor
    public static class WebhookEventRow {
        @Id @Column(name = "event_id", nullable = false, updatable = false) private UUID eventId;
        @Column(name = "provider", length = 40) private String provider;
        @Column(name = "provider_event_id", nullable = false, length = 160) private String providerEventId;
        @Column(name = "event_type", length = 120) private String eventType;
        @Column(name = "payload", columnDefinition = "text") private String payload;
        @Column(name = "processed", nullable = false) private boolean processed;
        @Column(name = "received_at", nullable = false) private Instant receivedAt;
        @Column(name = "processed_at") private Instant processedAt;
    }
}
