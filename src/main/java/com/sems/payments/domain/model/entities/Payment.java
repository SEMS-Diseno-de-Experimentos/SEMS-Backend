package com.sems.payments.domain.model.entities;

import com.sems.payments.domain.model.valueobjects.Money;
import com.sems.payments.domain.model.valueobjects.PaymentStatus;
import java.time.Instant;
import java.util.UUID;
import lombok.Getter;

/** Cobro asociado a una suscripcion. */
@Getter
public class Payment {

    private final UUID paymentId;
    private final UUID subscriptionId;
    private final UUID userId;
    private final UUID paymentMethodId;
    private final double amount;
    private final String currency;
    private PaymentStatus status;
    private final String paymentMethod;
    private String stripePaymentIntentId;
    private Instant paidAt;
    private final Instant createdAt;

    public Payment(UUID paymentId, UUID subscriptionId, UUID userId, UUID paymentMethodId,
                   double amount, String currency, PaymentStatus status, String paymentMethod,
                   String stripePaymentIntentId, Instant paidAt, Instant createdAt) {
        this.paymentId = paymentId;
        this.subscriptionId = subscriptionId;
        this.userId = userId;
        this.paymentMethodId = paymentMethodId;
        this.amount = amount;
        this.currency = currency;
        this.status = status;
        this.paymentMethod = paymentMethod;
        this.stripePaymentIntentId = stripePaymentIntentId;
        this.paidAt = paidAt;
        this.createdAt = createdAt;
    }

    /** El importe pasa por {@link Money}, que garantiza que sea positivo. */
    public static Payment create(UUID subscriptionId, UUID userId, UUID paymentMethodId,
                                 double amount, String currency, String paymentMethod) {
        Money money = new Money(amount, currency);
        return new Payment(UUID.randomUUID(), subscriptionId, userId, paymentMethodId,
                money.amount(), money.currency(), PaymentStatus.PENDING, paymentMethod,
                null, null, Instant.now());
    }

    public void markProcessing(String stripePaymentIntentId) {
        this.status = PaymentStatus.PROCESSING;
        this.stripePaymentIntentId = stripePaymentIntentId;
    }

    public void markProcessed(String stripePaymentIntentId) {
        this.status = PaymentStatus.PROCESSED;
        this.stripePaymentIntentId = stripePaymentIntentId;
        this.paidAt = Instant.now();
    }

    public void markFailed(String stripePaymentIntentId) {
        this.status = PaymentStatus.FAILED;
        this.stripePaymentIntentId = stripePaymentIntentId;
    }

    public void markCancelled(String stripePaymentIntentId) {
        this.status = PaymentStatus.CANCELLED;
        this.stripePaymentIntentId = stripePaymentIntentId;
    }

    public boolean isPaid() {
        return this.status == PaymentStatus.PROCESSED;
    }
}
