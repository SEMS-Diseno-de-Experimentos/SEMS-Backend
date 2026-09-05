package com.sems.subscriptions.domain.repositories;

import com.sems.subscriptions.domain.model.entities.Subscription;
import com.sems.subscriptions.domain.model.entities.SubscriptionPlan;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

/** Puertos de salida del modulo de suscripciones. */
public final class SubscriptionRepositories {

    private SubscriptionRepositories() {
    }

    public interface PlanRepository {
        SubscriptionPlan save(SubscriptionPlan plan);
        Optional<SubscriptionPlan> findById(UUID planId);
        Optional<SubscriptionPlan> findByName(String name);
        List<SubscriptionPlan> findAllActive();
        long count();
    }

    public interface SubscriptionRepository {
        Subscription save(Subscription subscription);
        Optional<Subscription> findById(UUID subscriptionId);
        List<Subscription> findByUserId(String userId);
        Optional<Subscription> findByStripeSubscriptionId(String stripeSubscriptionId);
    }
}
