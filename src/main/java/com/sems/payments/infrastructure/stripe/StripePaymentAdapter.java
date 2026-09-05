package com.sems.payments.infrastructure.stripe;

import com.sems.payments.domain.services.PaymentProvider;
import com.sems.shared.errors.AppException;
import com.stripe.Stripe;
import com.stripe.exception.SignatureVerificationException;
import com.stripe.exception.StripeException;
import com.stripe.model.Event;
import com.stripe.model.PaymentIntent;
import com.stripe.model.StripeObject;
import com.stripe.model.checkout.Session;
import com.stripe.net.Webhook;
import com.stripe.param.PaymentIntentCreateParams;
import com.stripe.param.checkout.SessionCreateParams;
import jakarta.annotation.PostConstruct;
import java.util.Map;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

/**
 * Adaptador de Stripe.
 *
 * <p>Es el unico punto del sistema que conoce el SDK de Stripe. Traduce entre
 * sus tipos y los del puerto {@link PaymentProvider}, de modo que ni el dominio
 * ni la capa de aplicacion dependen del proveedor.
 */
@Slf4j
@Component
public class StripePaymentAdapter implements PaymentProvider {

    @Value("${stripe.secret-key:}")
    private String secretKey;

    @Value("${stripe.webhook-secret:}")
    private String webhookSecret;

    @Value("${stripe.currency:pen}")
    private String defaultCurrency;

    @PostConstruct
    void init() {
        if (secretKey == null || secretKey.isBlank()) {
            log.warn("STRIPE_SECRET_KEY no esta configurada; los cobros fallaran");
            return;
        }
        Stripe.apiKey = secretKey;
    }

    @Override
    public PaymentMethodDetails getPaymentMethodDetails(String stripePaymentMethodId) {
        try {
            com.stripe.model.PaymentMethod method =
                    com.stripe.model.PaymentMethod.retrieve(stripePaymentMethodId);
            var card = method.getCard();
            if (card == null) {
                return new PaymentMethodDetails(method.getType(), "", "", 0, 0);
            }
            return new PaymentMethodDetails(method.getType(), card.getBrand(), card.getLast4(),
                    card.getExpMonth() == null ? 0 : card.getExpMonth().intValue(),
                    card.getExpYear() == null ? 0 : card.getExpYear().intValue());
        } catch (StripeException e) {
            throw providerFailure("no se pudo leer el medio de pago", e);
        }
    }

    @Override
    public PaymentIntentResult createPaymentIntent(CreatePaymentIntentRequest request) {
        try {
            PaymentIntentCreateParams params = PaymentIntentCreateParams.builder()
                    .setAmount(Math.round(request.amount() * 100))
                    .setCurrency(currencyOrDefault(request.currency()))
                    .setPaymentMethod(request.stripePaymentMethodId())
                    .setConfirm(true)
                    // Sin esto, una tarjeta que pide autenticacion deja el cobro
                    // colgado esperando una redireccion que nadie va a completar.
                    .setAutomaticPaymentMethods(
                            PaymentIntentCreateParams.AutomaticPaymentMethods.builder()
                                    .setEnabled(true)
                                    .setAllowRedirects(
                                            PaymentIntentCreateParams.AutomaticPaymentMethods
                                                    .AllowRedirects.NEVER)
                                    .build())
                    .putMetadata("user_id", nullSafe(request.userId()))
                    .putMetadata("subscription_id", nullSafe(request.subscriptionId()))
                    .putMetadata("payment_id", nullSafe(request.paymentId()))
                    .build();

            PaymentIntent intent = PaymentIntent.create(params);
            return new PaymentIntentResult(intent.getId(), intent.getStatus());
        } catch (StripeException e) {
            throw providerFailure("no se pudo iniciar el cobro", e);
        }
    }

    @Override
    public PaymentIntentResult confirmPaymentIntent(String paymentIntentId) {
        try {
            PaymentIntent intent = PaymentIntent.retrieve(paymentIntentId);
            return new PaymentIntentResult(intent.getId(), intent.getStatus());
        } catch (StripeException e) {
            throw providerFailure("no se pudo confirmar el cobro", e);
        }
    }

    /**
     * Crea una sesion de Stripe Checkout.
     *
     * <p>El usuario paga en una pagina alojada por Stripe, asi que los datos de
     * la tarjeta nunca pasan por nuestros servidores. Los identificadores viajan
     * como metadatos para poder correlacionar el webhook con nuestro registro.
     */
    @Override
    public CheckoutSessionResult createCheckoutSession(CreateCheckoutSessionRequest request) {
        try {
            SessionCreateParams params = SessionCreateParams.builder()
                    .setMode(SessionCreateParams.Mode.PAYMENT)
                    .setSuccessUrl(request.successUrl())
                    .setCancelUrl(request.cancelUrl())
                    .addLineItem(SessionCreateParams.LineItem.builder()
                            .setQuantity(1L)
                            .setPriceData(SessionCreateParams.LineItem.PriceData.builder()
                                    .setCurrency(currencyOrDefault(request.currency()))
                                    .setUnitAmount(Math.round(request.amount() * 100))
                                    .setProductData(SessionCreateParams.LineItem.PriceData
                                            .ProductData.builder()
                                            .setName(request.planName() == null
                                                    ? "Suscripcion SEMS" : request.planName())
                                            .build())
                                    .build())
                            .build())
                    .putMetadata("user_id", nullSafe(request.userId()))
                    .putMetadata("subscription_id", nullSafe(request.subscriptionId()))
                    .build();

            Session session = Session.create(params);
            return new CheckoutSessionResult(session.getId(), session.getUrl());
        } catch (StripeException e) {
            throw providerFailure("no se pudo crear la sesion de pago", e);
        }
    }

    @Override
    public ProviderWebhookEvent parseWebhookEvent(String payload, String signature) {
        if (webhookSecret == null || webhookSecret.isBlank()) {
            throw AppException.internal("stripe webhook is not configured");
        }
        Event event;
        try {
            event = Webhook.constructEvent(payload, signature, webhookSecret);
        } catch (SignatureVerificationException e) {
            // Firma invalida: alguien esta enviando eventos que no vienen de Stripe.
            throw AppException.validation("invalid stripe signature");
        }

        StripeObject object = event.getDataObjectDeserializer().getObject().orElse(null);

        if (object instanceof Session session) {
            Map<String, String> metadata = session.getMetadata() == null
                    ? Map.of() : session.getMetadata();
            double amount = session.getAmountTotal() == null ? 0.0
                    : session.getAmountTotal() / 100.0;
            return new ProviderWebhookEvent(event.getId(), event.getType(), payload,
                    session.getPaymentIntent(), "succeeded",
                    session.getId(), metadata.get("user_id"), metadata.get("subscription_id"),
                    amount, session.getCurrency());
        }

        if (object instanceof PaymentIntent intent) {
            return new ProviderWebhookEvent(event.getId(), event.getType(), payload,
                    intent.getId(), intent.getStatus(),
                    null, null, null, null, null);
        }

        return new ProviderWebhookEvent(event.getId(), event.getType(), payload,
                null, null, null, null, null, null, null);
    }

    private String currencyOrDefault(String currency) {
        return currency == null || currency.isBlank()
                ? defaultCurrency : currency.trim().toLowerCase();
    }

    private static String nullSafe(String value) {
        return value == null ? "" : value;
    }

    private AppException providerFailure(String message, StripeException e) {
        log.error("Stripe: {} ({})", message, e.getMessage());
        return AppException.internal("external provider error: " + message);
    }
}
