package com.sems.subscriptions.infrastructure.persistence.jpa.entities;

import com.sems.subscriptions.domain.model.valueobjects.SubscriptionStatus;
import jakarta.persistence.*;
import java.time.Instant;
import java.util.UUID;
import lombok.*;

/** Filas de las tablas del modulo de suscripciones. */
public final class SubscriptionJpaEntities {

    private SubscriptionJpaEntities() {
    }

    @Entity
    @Table(name = "sb_subscription_plans",
            uniqueConstraints = @UniqueConstraint(name = "uk_sb_plan_name", columnNames = "name"))
    @Getter @Setter @NoArgsConstructor @AllArgsConstructor
    public static class PlanRow {
        @Id @Column(name = "plan_id", nullable = false, updatable = false) private UUID planId;
        @Column(name = "name", nullable = false, length = 80) private String name;
        @Column(name = "description", columnDefinition = "text") private String description;
        @Column(name = "price", nullable = false) private double price;
        @Column(name = "currency", length = 10) private String currency;
        @Column(name = "billing_period", length = 40) private String billingPeriod;
        @Column(name = "active", nullable = false) private boolean active;
        @Column(name = "created_at", nullable = false) private Instant createdAt;
    }

    @Entity
    @Table(name = "sb_plan_features",
            indexes = @Index(name = "idx_sb_feature_plan", columnList = "plan_id"))
    @Getter @Setter @NoArgsConstructor @AllArgsConstructor
    public static class FeatureRow {
        @Id @Column(name = "feature_id", nullable = false, updatable = false) private UUID featureId;
        @Column(name = "plan_id", nullable = false) private UUID planId;
        @Column(name = "feature_code", length = 80) private String featureCode;
        @Column(name = "feature_name", length = 200) private String featureName;
        @Column(name = "feature_value", length = 200) private String featureValue;
        @Column(name = "created_at", nullable = false) private Instant createdAt;
    }

    @Entity
    @Table(name = "sb_subscriptions",
            indexes = {
                    @Index(name = "idx_sb_sub_user", columnList = "user_id"),
                    @Index(name = "idx_sb_sub_stripe", columnList = "stripe_subscription_id")
            })
    @Getter @Setter @NoArgsConstructor @AllArgsConstructor
    public static class SubscriptionRow {
        @Id @Column(name = "subscription_id", nullable = false, updatable = false) private UUID subscriptionId;
        @Column(name = "user_id", nullable = false, length = 80) private String userId;
        @Column(name = "plan_id", nullable = false) private UUID planId;
        @Enumerated(EnumType.STRING)
        @Column(name = "status", nullable = false, length = 30) private SubscriptionStatus status;
        @Column(name = "start_date", nullable = false) private Instant startDate;
        @Column(name = "end_date") private Instant endDate;
        @Column(name = "stripe_subscription_id", length = 120) private String stripeSubscriptionId;
        @Column(name = "created_at", nullable = false) private Instant createdAt;
    }
}
