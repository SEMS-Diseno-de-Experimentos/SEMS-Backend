package com.sems.payments.application;

import com.sems.payments.domain.model.entities.*;
import com.sems.payments.domain.repositories.PaymentRepositories.*;
import com.sems.shared.errors.AppException;
import java.util.List;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** Consultas del modulo de pagos. */
@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class PaymentQueryService {

    private final PaymentRepository payments;
    private final PaymentMethodRepository methods;
    private final InvoiceRepository invoices;

    public Payment paymentById(UUID paymentId) {
        return payments.findById(paymentId)
                .orElseThrow(() -> AppException.notFound("payment not found"));
    }

    public List<Payment> paymentsByUser(UUID userId) {
        return payments.findByUserId(userId);
    }

    public List<Payment> paymentsBySubscription(UUID subscriptionId) {
        return payments.findBySubscriptionId(subscriptionId);
    }

    public List<PaymentMethod> methodsByUser(UUID userId) {
        return methods.findByUserId(userId);
    }

    public Invoice invoiceById(UUID invoiceId) {
        return invoices.findById(invoiceId)
                .orElseThrow(() -> AppException.notFound("invoice not found"));
    }

    public Invoice invoiceByPayment(UUID paymentId) {
        return invoices.findByPaymentId(paymentId)
                .orElseThrow(() -> AppException.notFound("invoice not found"));
    }
}
