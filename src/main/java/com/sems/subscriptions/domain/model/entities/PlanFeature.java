package com.sems.subscriptions.domain.model.entities;

import java.time.Instant;
import java.util.UUID;
import lombok.Getter;

/**
 * Caracteristica incluida en un plan.
 *
 * <p>El valor es texto libre para poder expresar tanto interruptores
 * ({@code "enabled"}) como limites numericos ({@code "3"}), que es como el
 * frontend decide cuantos dispositivos permite cada plan.
 */
@Getter
public class PlanFeature {

    public static final String STRIPE_PRICE_ID = "STRIPE_PRICE_ID";

    private final UUID featureId;
    private final UUID planId;
    private final String featureCode;
    private final String featureName;
    private final String featureValue;
    private final Instant createdAt;

    public PlanFeature(UUID featureId, UUID planId, String featureCode, String featureName,
                       String featureValue, Instant createdAt) {
        this.featureId = featureId;
        this.planId = planId;
        this.featureCode = featureCode;
        this.featureName = featureName;
        this.featureValue = featureValue;
        this.createdAt = createdAt;
    }

    public static PlanFeature create(UUID planId, String code, String name, String value) {
        return new PlanFeature(UUID.randomUUID(), planId, code, name, value, Instant.now());
    }
}
