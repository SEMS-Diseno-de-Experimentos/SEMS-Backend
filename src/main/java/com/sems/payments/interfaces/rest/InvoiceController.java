package com.sems.payments.interfaces.rest;

import com.sems.payments.application.PaymentQueryService;
import com.sems.payments.interfaces.rest.resources.PaymentResources.InvoiceResponse;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import org.springframework.web.bind.annotation.*;

/** Comprobantes emitidos por cada cobro completado. */
@Tag(name = "Invoices", description = "Comprobantes de pago")
@RestController
@RequestMapping("/api/v1/invoices")
@RequiredArgsConstructor
public class InvoiceController {

    private final PaymentQueryService queries;

    @Operation(summary = "Obtiene un comprobante por su identificador")
    @GetMapping("/{invoiceId}")
    public InvoiceResponse byId(@PathVariable UUID invoiceId) {
        return InvoiceResponse.from(queries.invoiceById(invoiceId));
    }

    @Operation(summary = "Comprobante asociado a un pago")
    @GetMapping("/payment/{paymentId}")
    public InvoiceResponse byPayment(@PathVariable UUID paymentId) {
        return InvoiceResponse.from(queries.invoiceByPayment(paymentId));
    }
}
