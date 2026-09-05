package com.sems.payments.interfaces.rest.resources;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.databind.PropertyNamingStrategies;
import com.fasterxml.jackson.databind.annotation.JsonNaming;
import com.sems.payments.domain.model.entities.*;
import com.sems.payments.domain.model.valueobjects.PaymentStatus;
import jakarta.validation.constraints.NotBlank;
import java.time.Instant;

/**
 * Contrato JSON del modulo de pagos, en snake_case como el servicio original.
 */
public final class PaymentResources {

    private PaymentResources() {
    }

    // ------------------------------------------------------------- peticiones

    @JsonNaming(PropertyNamingStrategies.SnakeCaseStrategy.class)
    public record ProcessPaymentRequest(
            @NotBlank(message = "is required") String subscriptionId,
            @NotBlank(message = "is required") String userId,
            @NotBlank(message = "is required") String paymentMethodId,
            double amount,
            String currency,
            String paymentMethod) {
    }

    @JsonNaming(PropertyNamingStrategies.SnakeCaseStrategy.class)
    public record RegisterPaymentMethodRequest(
            @NotBlank(message = "is required") String userId,
            String type,
            @NotBlank(message = "is required") String stripePaymentMethodId,
            boolean isDefault) {
    }

    /** Cuerpo que envia la aplicacion web para abrir Stripe Checkout. */
    @JsonNaming(PropertyNamingStrategies.SnakeCaseStrategy.class)
    public record CreateCheckoutRequest(
            @NotBlank(message = "is required") String userId,
            String subscriptionId,
            String planName,
            double amount,
            String currency,
            @NotBlank(message = "is required") String successUrl,
            @NotBlank(message = "is required") String cancelUrl) {
    }

    // -------------------------------------------------------------- respuestas

    @JsonInclude(JsonInclude.Include.NON_NULL)
    @JsonNaming(PropertyNamingStrategies.SnakeCaseStrategy.class)
    public record PaymentResponse(
            String paymentId, String subscriptionId, String userId, String paymentMethodId,
            double amount, String currency, PaymentStatus status, String paymentMethod,
            String stripePaymentIntentId, Instant paidAt, Instant createdAt) {

        public static PaymentResponse from(Payment p) {
            return new PaymentResponse(p.getPaymentId().toString(),
                    p.getSubscriptionId() == null ? null : p.getSubscriptionId().toString(),
                    p.getUserId().toString(),
                    p.getPaymentMethodId() == null ? null : p.getPaymentMethodId().toString(),
                    p.getAmount(), p.getCurrency(), p.getStatus(), p.getPaymentMethod(),
                    p.getStripePaymentIntentId(), p.getPaidAt(), p.getCreatedAt());
        }
    }

    @JsonInclude(JsonInclude.Include.NON_NULL)
    @JsonNaming(PropertyNamingStrategies.SnakeCaseStrategy.class)
    public record InvoiceResponse(String invoiceId, String paymentId, String invoiceNumber,
                                  Instant issuedAt, double totalAmount, String pdfUrl) {

        public static InvoiceResponse from(Invoice i) {
            return new InvoiceResponse(i.getInvoiceId().toString(), i.getPaymentId().toString(),
                    i.getInvoiceNumber(), i.getIssuedAt(), i.getTotalAmount(), i.getPdfUrl());
        }
    }

    @JsonInclude(JsonInclude.Include.NON_NULL)
    @JsonNaming(PropertyNamingStrategies.SnakeCaseStrategy.class)
    public record ProcessPaymentResponse(PaymentResponse payment, InvoiceResponse invoice) {
    }

    @JsonInclude(JsonInclude.Include.NON_NULL)
    @JsonNaming(PropertyNamingStrategies.SnakeCaseStrategy.class)
    public record PaymentMethodResponse(
            String paymentMethodId, String userId, String type, String brand, String last4,
            int expMonth, int expYear, String stripePaymentMethodId, boolean isDefault,
            Instant createdAt) {

        public static PaymentMethodResponse from(PaymentMethod m) {
            return new PaymentMethodResponse(m.getPaymentMethodId().toString(),
                    m.getUserId().toString(), m.getType(), m.getBrand(), m.getLast4(),
                    m.getExpMonth(), m.getExpYear(), m.getStripePaymentMethodId(),
                    m.isDefaultMethod(), m.getCreatedAt());
        }
    }

    @JsonNaming(PropertyNamingStrategies.SnakeCaseStrategy.class)
    public record CheckoutSessionResponse(String sessionId, String url) {
    }
}
