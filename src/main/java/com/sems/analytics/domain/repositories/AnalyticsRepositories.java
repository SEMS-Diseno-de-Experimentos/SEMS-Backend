package com.sems.analytics.domain.repositories;

import com.sems.analytics.domain.model.entities.*;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

/**
 * Puertos de salida del modulo de analitica.
 *
 * <p>Los cinco agregados de este contexto se leen siempre igual: por usuario y
 * de lo mas reciente a lo mas antiguo. Se agrupan aqui para que el contrato del
 * modulo se lea de un vistazo.
 */
public final class AnalyticsRepositories {

    private AnalyticsRepositories() {
    }

    public interface BillPredictionRepository {
        BillPrediction save(BillPrediction prediction);
        List<BillPrediction> findByUserId(String userId);
    }

    public interface RecommendationRepository {
        Recommendation save(Recommendation recommendation);
        Optional<Recommendation> findById(UUID id);
        List<Recommendation> findByUserId(String userId);
    }

    public interface AnomalyRepository {
        Anomaly save(Anomaly anomaly);
        Optional<Anomaly> findById(UUID id);
        List<Anomaly> findByUserId(String userId);
    }

    public interface DeviceIdentificationRepository {
        DeviceIdentificationResult save(DeviceIdentificationResult result);
        List<DeviceIdentificationResult> findByUserId(String userId);
    }

    public interface ConsumptionRankingRepository {
        ConsumptionRanking save(ConsumptionRanking ranking);
        List<ConsumptionRanking> findByUserId(String userId);
    }
}
