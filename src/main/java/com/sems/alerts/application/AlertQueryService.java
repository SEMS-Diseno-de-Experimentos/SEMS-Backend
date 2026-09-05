package com.sems.alerts.application;

import com.sems.alerts.domain.model.entities.*;
import com.sems.alerts.domain.repositories.AlertRepositories.*;
import com.sems.shared.errors.AppException;
import java.util.List;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** Consultas del modulo de alertas. */
@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class AlertQueryService {

    private final AlertRepository alerts;
    private final ThresholdRepository thresholds;
    private final InactivityRuleRepository rules;
    private final NotificationPreferenceRepository preferences;

    public List<Alert> allAlerts() {
        return alerts.findAll();
    }

    public Alert alertById(UUID alertId) {
        return alerts.findById(alertId)
                .orElseThrow(() -> AppException.notFound("alert not found"));
    }

    public List<Alert> alertsByUser(UUID userId) {
        return alerts.findByUserId(userId);
    }

    public List<AlertThreshold> thresholdsByUser(UUID userId) {
        return thresholds.findByUserId(userId);
    }

    public List<InactivityRule> rulesByUser(UUID userId) {
        return rules.findByUserId(userId);
    }

    public List<NotificationPreference> preferencesByUser(UUID userId) {
        return preferences.findByUserId(userId);
    }
}
