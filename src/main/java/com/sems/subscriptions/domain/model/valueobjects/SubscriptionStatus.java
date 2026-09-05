package com.sems.subscriptions.domain.model.valueobjects;

import com.sems.shared.errors.AppException;

/** Estados posibles de una suscripcion. */
public enum SubscriptionStatus {
    ACTIVE,
    INACTIVE,
    CANCELLED,
    PENDING_RENEWAL,
    EXPIRED;

    public static SubscriptionStatus of(String value) {
        if (value == null) {
            throw AppException.validation("invalid subscription status");
        }
        try {
            return SubscriptionStatus.valueOf(value.trim().toUpperCase());
        } catch (IllegalArgumentException e) {
            throw AppException.validation("invalid subscription status");
        }
    }

    /** Una suscripcion cancelada o vencida ya no admite cambios. */
    public boolean isFinal() {
        return this == CANCELLED || this == EXPIRED;
    }
}
