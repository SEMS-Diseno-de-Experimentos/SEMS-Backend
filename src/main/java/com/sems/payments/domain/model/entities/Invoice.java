package com.sems.payments.domain.model.entities;

import java.time.Instant;
import java.time.ZoneOffset;
import java.time.format.DateTimeFormatter;
import java.util.UUID;
import lombok.Getter;

/** Comprobante emitido cuando un cobro se completa. */
@Getter
public class Invoice {

    private static final DateTimeFormatter DAY =
            DateTimeFormatter.ofPattern("yyyyMMdd").withZone(ZoneOffset.UTC);

    private final UUID invoiceId;
    private final UUID paymentId;
    private final String invoiceNumber;
    private final Instant issuedAt;
    private final double totalAmount;
    private final String pdfUrl;

    public Invoice(UUID invoiceId, UUID paymentId, String invoiceNumber, Instant issuedAt,
                   double totalAmount, String pdfUrl) {
        this.invoiceId = invoiceId;
        this.paymentId = paymentId;
        this.invoiceNumber = invoiceNumber;
        this.issuedAt = issuedAt;
        this.totalAmount = totalAmount;
        this.pdfUrl = pdfUrl;
    }

    /**
     * El numero de comprobante se compone de la fecha y el primer bloque del
     * identificador, igual que en el servicio original: {@code INV-20260904-A1B2C3D4}.
     */
    public static Invoice issueFor(UUID paymentId, double totalAmount, String pdfUrl) {
        UUID invoiceId = UUID.randomUUID();
        Instant now = Instant.now();
        String shortId = invoiceId.toString().split("-")[0].toUpperCase();
        String number = "INV-" + DAY.format(now) + "-" + shortId;
        return new Invoice(invoiceId, paymentId, number, now, totalAmount, pdfUrl);
    }
}
