package com.sems.payments;

import static org.junit.jupiter.api.Assertions.*;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule;
import com.sems.payments.domain.model.entities.Invoice;
import com.sems.payments.domain.model.entities.Payment;
import com.sems.payments.domain.model.entities.PaymentWebhookEvent;
import com.sems.payments.domain.model.valueobjects.Money;
import com.sems.payments.domain.model.valueobjects.PaymentStatus;
import com.sems.payments.domain.services.PaymentStatusMapper;
import com.sems.payments.interfaces.rest.resources.PaymentResources.PaymentResponse;
import com.sems.shared.errors.AppException;
import java.util.UUID;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class PaymentDomainTest {

    private final ObjectMapper mapper = new ObjectMapper().registerModule(new JavaTimeModule());

    @Test
    @DisplayName("Un importe cero o negativo no puede existir")
    void moneyRejectsNonPositiveAmounts() {
        assertThrows(AppException.class, () -> new Money(0, "PEN"));
        assertThrows(AppException.class, () -> new Money(-5, "PEN"));
        assertThrows(AppException.class, () -> new Money(10, "  "));
    }

    @Test
    @DisplayName("La moneda se normaliza a minusculas, que es lo que espera Stripe")
    void moneyNormalisesCurrency() {
        assertEquals("pen", new Money(10, " PEN ").currency());
        assertEquals(1550L, new Money(15.50, "pen").toMinorUnits());
    }

    @Test
    @DisplayName("Un estado desconocido de Stripe nunca se interpreta como cobrado")
    void unknownStripeStatusIsNeverProcessed() {
        PaymentStatusMapper mapper = new PaymentStatusMapper();

        assertEquals(PaymentStatus.PROCESSED, mapper.fromStripe("succeeded"));
        assertEquals(PaymentStatus.FAILED, mapper.fromStripe("payment_failed"));
        assertEquals(PaymentStatus.CANCELLED, mapper.fromStripe("canceled"));
        assertEquals(PaymentStatus.PROCESSING, mapper.fromStripe("algo_que_no_conocemos"));
        assertEquals(PaymentStatus.PROCESSING, mapper.fromStripe(null));
    }

    @Test
    @DisplayName("Un pago nace pendiente y solo al completarse sella la fecha")
    void paymentLifecycle() {
        Payment payment = Payment.create(UUID.randomUUID(), UUID.randomUUID(), UUID.randomUUID(),
                25.0, "PEN", "card");

        assertEquals(PaymentStatus.PENDING, payment.getStatus());
        assertNull(payment.getPaidAt());
        assertFalse(payment.isPaid());

        payment.markProcessed("pi_123");

        assertTrue(payment.isPaid());
        assertNotNull(payment.getPaidAt());
        assertEquals("pi_123", payment.getStripePaymentIntentId());
    }

    @Test
    @DisplayName("El numero de comprobante lleva fecha e identificador corto")
    void invoiceNumberFormat() {
        Invoice invoice = Invoice.issueFor(UUID.randomUUID(), 15.0, "");

        assertTrue(invoice.getInvoiceNumber().matches("INV-\\d{8}-[0-9A-F]{8}"),
                "formato inesperado: " + invoice.getInvoiceNumber());
    }

    @Test
    @DisplayName("Un evento de webhook se marca procesado con su fecha")
    void webhookEventMarksProcessed() {
        PaymentWebhookEvent event = PaymentWebhookEvent.received("stripe", "evt_1",
                "checkout.session.completed", "{}");

        assertFalse(event.isProcessed());
        assertNull(event.getProcessedAt());

        event.markProcessed();

        assertTrue(event.isProcessed());
        assertNotNull(event.getProcessedAt());
    }

    @Test
    @DisplayName("La respuesta de pago se serializa en snake_case")
    void paymentResponseUsesSnakeCase() throws Exception {
        Payment payment = Payment.create(UUID.randomUUID(), UUID.randomUUID(), UUID.randomUUID(),
                25.0, "PEN", "card");

        String json = mapper.writeValueAsString(PaymentResponse.from(payment));

        assertTrue(json.contains("\"payment_id\""), "falta payment_id: " + json);
        assertTrue(json.contains("\"payment_method_id\""), "falta payment_method_id");
        assertTrue(json.contains("\"created_at\""), "falta created_at");
        assertTrue(json.contains("\"pending\""), "el estado va en minusculas");
        assertFalse(json.contains("\"paidAt\""), "no debe salir camelCase");
    }
}
