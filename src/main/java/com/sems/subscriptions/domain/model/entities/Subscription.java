package com.sems.subscriptions.domain.model.entities;

import com.sems.subscriptions.domain.model.valueobjects.SubscriptionStatus;
import java.time.Instant;
import java.util.UUID;
import lombok.Getter;

/**
 * Suscripcion de un usuario a un plan.
 *
 * <p>{@code endDate} y {@code stripeSubscriptionId} son opcionales: el primero
 * solo se llena al cancelar o vencer, y el segundo queda nulo cuando la
 * suscripcion no esta enlazada a Stripe.
 */
@Getter
public class Subscription {

    private final UUID subscriptionId;
    private final String userId;
    private UUID planId;
    private SubscriptionStatus status;
    private final Instant startDate;
    private Instant endDate;
    private String stripeSubscriptionId;
    private final Instant createdAt;

    public Subscription(UUID subscriptionId, String userId, UUID planId, SubscriptionStatus status,
                        Instant startDate, Instant endDate, String stripeSubscriptionId,
                        Instant createdAt) {
        this.subscriptionId = subscriptionId;
        this.userId = userId;
        this.planId = planId;
        this.status = status;
        this.startDate = startDate;
        this.endDate = endDate;
        this.stripeSubscriptionId = stripeSubscriptionId;
        this.createdAt = createdAt;
    }

    public static Subscription start(String userId, UUID planId, String stripeSubscriptionId) {
        Instant now = Instant.now();
        return new Subscription(UUID.randomUUID(), userId, planId, SubscriptionStatus.ACTIVE,
                now, null, stripeSubscriptionId, now);
    }

    public void cancel() {
        this.status = SubscriptionStatus.CANCELLED;
        this.endDate = Instant.now();
    }

    public void changePlan(UUID newPlanId) {
        this.planId = newPlanId;
    }

    public void updateStatus(SubscriptionStatus next) {
        this.status = next;
    }

    public void linkToStripe(String stripeSubscriptionId) {
        this.stripeSubscriptionId = stripeSubscriptionId;
    }
}
