package com.sems.subscriptions;

import static org.junit.jupiter.api.Assertions.*;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule;
import com.sems.shared.errors.AppException;
import com.sems.subscriptions.domain.model.entities.Subscription;
import com.sems.subscriptions.domain.model.entities.SubscriptionPlan;
import com.sems.subscriptions.domain.model.valueobjects.SubscriptionStatus;
import com.sems.subscriptions.domain.services.SubscriptionManager;
import com.sems.subscriptions.interfaces.rest.resources.SubscriptionResources.*;
import java.util.UUID;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

/**
 * El modulo de suscripciones tiene un contrato mixto heredado del servicio en
 * Go: peticiones en snake_case y respuestas en PascalCase. Estas pruebas lo
 * fijan, porque es la clase de detalle que se rompe sin que nadie lo note hasta
 * que la pantalla de planes aparece vacia.
 */
class SubscriptionContractTest {

    private final ObjectMapper mapper = new ObjectMapper().registerModule(new JavaTimeModule());

    @Test
    @DisplayName("La respuesta del plan se serializa en PascalCase")
    void planResponseUsesPascalCase() throws Exception {
        SubscriptionPlan plan = SubscriptionPlan.create("Plus", "For active homes", 15, "PEN", "monthly");

        String json = mapper.writeValueAsString(PlanResource.from(plan));

        assertTrue(json.contains("\"PlanID\""), "falta PlanID: " + json);
        assertTrue(json.contains("\"Name\""), "falta Name");
        assertTrue(json.contains("\"BillingPeriod\""), "falta BillingPeriod");
        assertTrue(json.contains("\"PlanFeatures\""), "falta PlanFeatures");
        assertFalse(json.contains("\"plan_id\""), "no debe salir snake_case");
        assertFalse(json.contains("\"planId\""), "no debe salir camelCase");
    }

    @Test
    @DisplayName("La respuesta de la suscripcion se serializa en PascalCase")
    void subscriptionResponseUsesPascalCase() throws Exception {
        Subscription subscription = Subscription.start("user-1", UUID.randomUUID(), null);

        String json = mapper.writeValueAsString(SubscriptionResource.from(subscription));

        assertTrue(json.contains("\"SubscriptionID\""), "falta SubscriptionID: " + json);
        assertTrue(json.contains("\"UserID\""), "falta UserID");
        assertTrue(json.contains("\"Status\""), "falta Status");
        assertTrue(json.contains("\"ACTIVE\""), "el estado va en mayusculas");
        assertFalse(json.contains("\"EndDate\""), "EndDate se omite mientras este vigente");
    }

    @Test
    @DisplayName("La peticion de creacion se lee en snake_case")
    void createRequestReadsSnakeCase() throws Exception {
        String body = """
                {"user_id":"u-1","plan_id":"p-1","stripe_customer_id":"cus_123"}
                """;

        CreateSubscriptionRequest request = mapper.readValue(body, CreateSubscriptionRequest.class);

        assertEquals("u-1", request.userId());
        assertEquals("p-1", request.planId());
        assertEquals("cus_123", request.stripeCustomerId());
    }

    @Test
    @DisplayName("Una suscripcion cancelada no se puede volver a cancelar ni cambiar de plan")
    void finalStatesAreGuarded() {
        SubscriptionManager manager = new SubscriptionManager();
        Subscription subscription = Subscription.start("u", UUID.randomUUID(), null);

        manager.ensureCanCancel(subscription.getStatus());
        subscription.cancel();

        assertEquals(SubscriptionStatus.CANCELLED, subscription.getStatus());
        assertNotNull(subscription.getEndDate());
        assertThrows(AppException.class, () -> manager.ensureCanCancel(subscription.getStatus()));
        assertThrows(AppException.class, () -> manager.ensureCanChangePlan(subscription.getStatus()));
    }

    @Test
    @DisplayName("El identificador de precio de Stripe se lee de las caracteristicas del plan")
    void stripePriceIdComesFromFeatures() {
        SubscriptionPlan plan = SubscriptionPlan.create("Pro", "d", 25, "PEN", "monthly");
        assertNull(plan.stripePriceId());

        SubscriptionPlan withPrice = new SubscriptionPlan(plan.getPlanId(), plan.getName(),
                plan.getDescription(), plan.getPrice(), plan.getCurrency(), plan.getBillingPeriod(),
                true, plan.getCreatedAt(),
                java.util.List.of(com.sems.subscriptions.domain.model.entities.PlanFeature.create(
                        plan.getPlanId(), "STRIPE_PRICE_ID", "Stripe price id", "price_abc")));

        assertEquals("price_abc", withPrice.stripePriceId());
    }
}
