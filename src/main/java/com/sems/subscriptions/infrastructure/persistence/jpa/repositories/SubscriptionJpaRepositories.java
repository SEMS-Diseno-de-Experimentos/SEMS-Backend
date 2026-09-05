package com.sems.subscriptions.infrastructure.persistence.jpa.repositories;

import com.sems.subscriptions.infrastructure.persistence.jpa.entities.SubscriptionJpaEntities.*;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

/** Repositorios de Spring Data del modulo de suscripciones. */
public final class SubscriptionJpaRepositories {

    private SubscriptionJpaRepositories() {
    }

    public interface PlanJpa extends JpaRepository<PlanRow, UUID> {
        Optional<PlanRow> findByName(String name);
        List<PlanRow> findByActiveTrueOrderByPriceAsc();
    }

    public interface FeatureJpa extends JpaRepository<FeatureRow, UUID> {
        List<FeatureRow> findByPlanId(UUID planId);
        void deleteByPlanId(UUID planId);
    }

    public interface SubscriptionJpa extends JpaRepository<SubscriptionRow, UUID> {
        List<SubscriptionRow> findByUserIdOrderByCreatedAtDesc(String userId);
        Optional<SubscriptionRow> findByStripeSubscriptionId(String stripeSubscriptionId);
    }
}
