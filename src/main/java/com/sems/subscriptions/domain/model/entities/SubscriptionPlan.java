package com.sems.subscriptions.domain.model.entities;

import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import lombok.Getter;

/** Plan comercial al que un usuario puede suscribirse. */
@Getter
public class SubscriptionPlan {

    private final UUID planId;
    private final String name;
    private final String description;
    private final double price;
    private final String currency;
    private final String billingPeriod;
    private final boolean active;
    private final Instant createdAt;
    private final List<PlanFeature> planFeatures;

    public SubscriptionPlan(UUID planId, String name, String description, double price,
                            String currency, String billingPeriod, boolean active,
                            Instant createdAt, List<PlanFeature> planFeatures) {
        this.planId = planId;
        this.name = name;
        this.description = description;
        this.price = price;
        this.currency = currency;
        this.billingPeriod = billingPeriod;
        this.active = active;
        this.createdAt = createdAt;
        this.planFeatures = planFeatures == null ? new ArrayList<>() : new ArrayList<>(planFeatures);
    }

    public static SubscriptionPlan create(String name, String description, double price,
                                          String currency, String billingPeriod) {
        return new SubscriptionPlan(UUID.randomUUID(), name, description, price,
                currency == null ? "PEN" : currency,
                billingPeriod == null ? "monthly" : billingPeriod,
                true, Instant.now(), new ArrayList<>());
    }

    /** Identificador del precio en Stripe, si el plan esta enlazado. */
    public String stripePriceId() {
        return planFeatures.stream()
                .filter(f -> PlanFeature.STRIPE_PRICE_ID.equalsIgnoreCase(f.getFeatureCode()))
                .map(PlanFeature::getFeatureValue)
                .filter(v -> v != null && !v.isBlank())
                .findFirst()
                .orElse(null);
    }
}
