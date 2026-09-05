package com.sems.subscriptions.domain.services;

import com.sems.subscriptions.domain.model.valueobjects.SubscriptionStatus;
import com.sems.shared.errors.AppException;
import org.springframework.stereotype.Service;

/**
 * Servicio de dominio que concentra las reglas de transicion.
 *
 * <p>No pertenecen a una sola entidad, asi que viven aqui en lugar de
 * dispersarse por la capa de aplicacion.
 */
@Service
public class SubscriptionManager {

    public void ensureCanCancel(SubscriptionStatus status) {
        if (status.isFinal()) {
            throw AppException.conflict("subscription cannot be cancelled from current status");
        }
    }

    public void ensureCanChangePlan(SubscriptionStatus status) {
        if (status.isFinal()) {
            throw AppException.conflict("subscription cannot change plan from current status");
        }
    }
}
