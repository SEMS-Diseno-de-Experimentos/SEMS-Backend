package com.sems.subscriptions.infrastructure.seed;

import com.sems.subscriptions.domain.model.entities.PlanFeature;
import com.sems.subscriptions.domain.model.entities.SubscriptionPlan;
import com.sems.subscriptions.domain.repositories.SubscriptionRepositories.PlanRepository;
import java.util.ArrayList;
import java.util.List;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.ApplicationArguments;
import org.springframework.boot.ApplicationRunner;
import org.springframework.stereotype.Component;

/**
 * Carga los tres planes por defecto la primera vez que arranca el sistema.
 *
 * <p>Porta el seeder del servicio en Go. Solo actua si no hay ningun plan, de
 * modo que reiniciar la aplicacion nunca duplica ni sobreescribe lo existente.
 *
 * <p>El limite de dispositivos vive como caracteristica del plan
 * ({@code LINKED_DEVICES_LIMIT}) y no en el codigo: asi se puede cambiar sin
 * volver a desplegar.
 */
@Slf4j
@Component
@RequiredArgsConstructor
public class PlanSeeder implements ApplicationRunner {

    private final PlanRepository plans;

    @Value("${stripe.price.free:}")
    private String stripePriceFree;

    @Value("${stripe.price.plus:}")
    private String stripePricePlus;

    @Value("${stripe.price.pro:}")
    private String stripePricePro;

    @Override
    public void run(ApplicationArguments args) {
        if (plans.count() > 0) {
            return;
        }
        log.info("No hay planes registrados; cargando los tres por defecto");

        plans.save(withFeatures(
                SubscriptionPlan.create("Free", "Start monitoring at no cost", 0, "PEN", "monthly"),
                stripePriceFree,
                feature("BASIC_DASHBOARD", "Basic energy dashboard", "enabled"),
                feature("CONSUMPTION_ALERTS", "Essential consumption alerts", "enabled"),
                feature("LINKED_DEVICES_LIMIT", "Linked devices limit", "3")));

        plans.save(withFeatures(
                SubscriptionPlan.create("Plus", "For active homes", 15, "PEN", "monthly"),
                stripePricePlus,
                feature("FREE_INCLUDED", "Everything in Free", "enabled"),
                feature("DEVICE_ANALYTICS", "Detailed device analytics", "enabled"),
                feature("SAVING_RECOMMENDATIONS", "Personalized saving recommendations", "enabled"),
                feature("MONTHLY_REPORTS", "Monthly savings reports", "enabled"),
                feature("LINKED_DEVICES_LIMIT", "Linked devices limit", "10")));

        plans.save(withFeatures(
                SubscriptionPlan.create("Pro", "Advanced control and insights", 25, "PEN", "monthly"),
                stripePricePro,
                feature("PLUS_INCLUDED", "Everything in Plus", "enabled"),
                feature("UNLIMITED_DEVICES", "Unlimited linked devices", "enabled"),
                feature("PRIORITY_SUPPORT", "Priority support", "enabled")));
    }

    /** Adjunta las caracteristicas al plan y, si existe, el precio de Stripe. */
    private SubscriptionPlan withFeatures(SubscriptionPlan plan, String stripePriceId,
                                          FeatureSpec... specs) {
        List<PlanFeature> features = new ArrayList<>();
        for (FeatureSpec spec : specs) {
            features.add(PlanFeature.create(plan.getPlanId(), spec.code(), spec.name(), spec.value()));
        }
        if (stripePriceId != null && !stripePriceId.isBlank()) {
            features.add(PlanFeature.create(plan.getPlanId(), PlanFeature.STRIPE_PRICE_ID,
                    "Stripe price id", stripePriceId));
        }
        return new SubscriptionPlan(plan.getPlanId(), plan.getName(), plan.getDescription(),
                plan.getPrice(), plan.getCurrency(), plan.getBillingPeriod(), plan.isActive(),
                plan.getCreatedAt(), features);
    }

    private static FeatureSpec feature(String code, String name, String value) {
        return new FeatureSpec(code, name, value);
    }

    private record FeatureSpec(String code, String name, String value) {
    }
}
