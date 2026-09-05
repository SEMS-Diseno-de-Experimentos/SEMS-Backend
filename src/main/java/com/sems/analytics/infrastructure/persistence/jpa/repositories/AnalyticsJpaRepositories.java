package com.sems.analytics.infrastructure.persistence.jpa.repositories;

import com.sems.analytics.infrastructure.persistence.jpa.entities.AnalyticsJpaEntities.*;
import java.util.List;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

/** Repositorios de Spring Data del modulo de analitica. */
public final class AnalyticsJpaRepositories {

    private AnalyticsJpaRepositories() {
    }

    public interface BillPredictionJpa extends JpaRepository<BillPredictionRow, UUID> {
        List<BillPredictionRow> findByUserIdOrderByGeneratedAtDesc(String userId);
    }

    public interface RecommendationJpa extends JpaRepository<RecommendationRow, UUID> {
        List<RecommendationRow> findByUserIdOrderByGeneratedAtDesc(String userId);
    }

    public interface AnomalyJpa extends JpaRepository<AnomalyRow, UUID> {
        List<AnomalyRow> findByUserIdOrderByDetectedAtDesc(String userId);
    }

    public interface DeviceIdentificationJpa extends JpaRepository<DeviceIdentificationRow, UUID> {
        List<DeviceIdentificationRow> findByUserIdOrderByAnalyzedAtDesc(String userId);
    }

    public interface ConsumptionRankingJpa extends JpaRepository<ConsumptionRankingRow, UUID> {
        List<ConsumptionRankingRow> findByUserIdOrderByGeneratedAtDesc(String userId);
    }
}
