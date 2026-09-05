package com.sems.payments.domain.services;

/**
 * Puerto hacia el procesador de pagos.
 *
 * <p>La capa de aplicacion depende solo de esta interfaz, nunca del SDK de
 * Stripe. Eso permite sustituir el proveedor o usar un doble en pruebas sin
 * tocar la logica de negocio.
 */
public interface PaymentProvider {

    /** Datos visibles de una tarjeta guardada: marca, ultimos digitos, vencimiento. */
    record PaymentMethodDetails(String type, String brand, String last4, int expMonth, int expYear) {
    }

    record CreatePaymentIntentRequest(double amount, String currency, String stripePaymentMethodId,
                                      String userId, String subscriptionId, String paymentId) {
    }

    record PaymentIntentResult(String id, String status) {
    }

    /** Solicitud de sesion de pago alojada por Stripe (Stripe Checkout). */
    record CreateCheckoutSessionRequest(String userId, String subscriptionId, String planName,
                                        double amount, String currency,
                                        String successUrl, String cancelUrl) {
    }

    record CheckoutSessionResult(String sessionId, String url) {
    }

    /** Evento de webhook ya verificado y normalizado por el adaptador. */
    record ProviderWebhookEvent(String providerEventId, String eventType, String payload,
                                String stripePaymentIntentId, String paymentStatus,
                                String checkoutSessionId, String checkoutUserId,
                                String checkoutSubscriptionId, Double checkoutAmount,
                                String checkoutCurrency) {
    }

    PaymentMethodDetails getPaymentMethodDetails(String stripePaymentMethodId);

    PaymentIntentResult createPaymentIntent(CreatePaymentIntentRequest request);

    PaymentIntentResult confirmPaymentIntent(String paymentIntentId);

    CheckoutSessionResult createCheckoutSession(CreateCheckoutSessionRequest request);

    /** Verifica la firma y traduce el evento. Lanza si la firma no cuadra. */
    ProviderWebhookEvent parseWebhookEvent(String payload, String signature);
}
