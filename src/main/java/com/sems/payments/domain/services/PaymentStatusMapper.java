package com.sems.payments.domain.services;

import com.sems.payments.domain.model.valueobjects.PaymentStatus;
import org.springframework.stereotype.Service;

/**
 * Traduce el vocabulario de estados de Stripe al del dominio.
 *
 * <p>Aisla al dominio del proveedor: si manana cambian sus nombres de estado,
 * solo cambia esta clase.
 */
@Service
public class PaymentStatusMapper {

    public PaymentStatus fromStripe(String status) {
        if (status == null) {
            return PaymentStatus.PROCESSING;
        }
        return switch (status) {
            case "succeeded" -> PaymentStatus.PROCESSED;
            case "requires_payment_method", "payment_failed" -> PaymentStatus.FAILED;
            case "canceled", "cancelled" -> PaymentStatus.CANCELLED;
            case "processing", "requires_confirmation", "requires_action" -> PaymentStatus.PROCESSING;
            // Un estado desconocido se trata como en curso, nunca como cobrado.
            default -> PaymentStatus.PROCESSING;
        };
    }
}
