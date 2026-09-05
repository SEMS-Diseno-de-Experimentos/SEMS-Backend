package com.sems.alerts.infrastructure.persistence.jpa.repositories;

import com.sems.alerts.infrastructure.persistence.jpa.entities.AlertJpaEntities.*;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

/** Repositorios de Spring Data del modulo de alertas. */
public final class AlertJpaRepositories {

    private AlertJpaRepositories() {
    }

    public interface AlertJpa extends JpaRepository<AlertRow, UUID> {
        List<AlertRow> findByUserIdOrderByTriggeredAtDesc(UUID userId);
        List<AlertRow> findAllByOrderByTriggeredAtDesc();
    }

    public interface ThresholdJpa extends JpaRepository<ThresholdRow, UUID> {
        List<ThresholdRow> findByUserIdOrderByCreatedAtDesc(UUID userId);
        List<ThresholdRow> findByDeviceIdAndActiveTrue(UUID deviceId);
        long countByUserId(UUID userId);
    }

    public interface InactivityRuleJpa extends JpaRepository<InactivityRuleRow, UUID> {
        List<InactivityRuleRow> findByUserIdOrderByCreatedAtDesc(UUID userId);
        List<InactivityRuleRow> findByActiveTrue();
    }

    public interface PreferenceJpa extends JpaRepository<PreferenceRow, UUID> {
        List<PreferenceRow> findByUserId(UUID userId);
        Optional<PreferenceRow> findByUserIdAndChannel(UUID userId, String channel);
    }

    public interface NotificationLogJpa extends JpaRepository<NotificationLogRow, UUID> {
        List<NotificationLogRow> findByAlertIdOrderByCreatedAtDesc(UUID alertId);
    }
}
