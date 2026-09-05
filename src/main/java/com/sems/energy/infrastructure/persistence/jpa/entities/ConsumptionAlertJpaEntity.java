package com.sems.energy.infrastructure.persistence.jpa.entities;

import com.sems.energy.domain.model.valueobjects.AlertSeverity;
import com.sems.energy.domain.model.valueobjects.AlertType;
import jakarta.persistence.*;
import java.time.Instant;
import java.util.UUID;
import lombok.*;

@Entity
@Table(name = "em_consumption_alerts",
        indexes = @Index(name = "idx_em_alerts_user", columnList = "user_id"))
@Getter @Setter @NoArgsConstructor @AllArgsConstructor
public class ConsumptionAlertJpaEntity {

    @Id
    @Column(name = "id", nullable = false, updatable = false)
    private UUID id;

    @Column(name = "user_id", nullable = false, length = 80)
    private String userId;

    @Column(name = "device_id", length = 80)
    private String deviceId;

    @Column(name = "meter_id", length = 80)
    private String meterId;

    @Enumerated(EnumType.STRING)
    @Column(name = "alert_type", nullable = false, length = 40)
    private AlertType alertType;

    @Enumerated(EnumType.STRING)
    @Column(name = "severity", nullable = false, length = 20)
    private AlertSeverity severity;

    @Column(name = "threshold_value", nullable = false)
    private double thresholdValue;

    @Column(name = "actual_value", nullable = false)
    private double actualValue;

    @Column(name = "message", columnDefinition = "text")
    private String message;

    @Column(name = "is_read", nullable = false)
    private boolean read;

    @Column(name = "is_resolved", nullable = false)
    private boolean resolved;

    @Column(name = "created_at", nullable = false)
    private Instant createdAt;

    @Column(name = "resolved_at")
    private Instant resolvedAt;
}
