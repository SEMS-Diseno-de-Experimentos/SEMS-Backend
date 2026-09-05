package com.sems.payments.application;

import com.sems.payments.domain.model.entities.PaymentMethod;
import com.sems.payments.domain.repositories.PaymentRepositories.PaymentMethodRepository;
import com.sems.payments.domain.services.PaymentProvider;
import com.sems.shared.errors.AppException;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** Alta y gestion de medios de pago del usuario. */
@Service
@RequiredArgsConstructor
public class PaymentMethodCommandService {

    private final PaymentMethodRepository methods;
    private final PaymentProvider provider;

    /**
     * Guarda una tarjeta.
     *
     * <p>Los datos visibles se leen de Stripe, no del cliente: asi la marca y los
     * ultimos digitos que mostramos son los reales y no lo que alguien decida
     * enviar en el cuerpo de la peticion.
     */
    @Transactional
    public PaymentMethod register(UUID userId, String type, String stripePaymentMethodId,
                                  boolean isDefault) {
        var details = provider.getPaymentMethodDetails(stripePaymentMethodId);

        boolean first = methods.findByUserId(userId).isEmpty();
        boolean shouldBeDefault = isDefault || first;

        if (shouldBeDefault) {
            clearCurrentDefault(userId);
        }

        return methods.save(PaymentMethod.create(userId,
                type == null || type.isBlank() ? details.type() : type,
                details.brand(), details.last4(), details.expMonth(), details.expYear(),
                stripePaymentMethodId, shouldBeDefault));
    }

    @Transactional
    public PaymentMethod setDefault(UUID paymentMethodId) {
        PaymentMethod method = methods.findById(paymentMethodId)
                .orElseThrow(() -> AppException.notFound("payment method not found"));
        clearCurrentDefault(method.getUserId());
        method.markDefault();
        return methods.save(method);
    }

    @Transactional
    public void delete(UUID paymentMethodId) {
        methods.findById(paymentMethodId)
                .orElseThrow(() -> AppException.notFound("payment method not found"));
        methods.deleteById(paymentMethodId);
    }

    /** Solo puede haber un medio de pago predeterminado por usuario. */
    private void clearCurrentDefault(UUID userId) {
        methods.findDefaultByUserId(userId).ifPresent(current -> {
            current.removeDefault();
            methods.save(current);
        });
    }
}
