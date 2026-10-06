using Sems.Api.Modules.Payments.Domain.Model;
using Sems.Api.Shared.Errors;
using Xunit;

namespace Sems.Api.Tests;

/// <summary>
/// Reglas del modulo de pagos que no dependen de Stripe ni de la base de datos.
///
/// <para>Son las que mas caro cuestan si se rompen: un importe negativo aceptado
/// o un cobro que salta de pendiente a pagado sin pasar por el proveedor son
/// errores de dinero, no de pantalla.</para>
/// </summary>
public class PaymentDomainTests
{
    [Fact]
    public void Un_importe_no_positivo_se_rechaza()
    {
        Assert.Equal(ErrorCode.VALIDATION_ERROR,
            Assert.Throws<AppException>(() => new Money(0, "pen")).Code);
        Assert.Equal(ErrorCode.VALIDATION_ERROR,
            Assert.Throws<AppException>(() => new Money(-5, "pen")).Code);
    }

    [Fact]
    public void La_moneda_se_normaliza_a_minusculas_para_Stripe()
    {
        var money = new Money(29.9, "  PEN  ");

        Assert.Equal("pen", money.Currency);
        Assert.Equal(29.9, money.Amount);
    }

    [Fact]
    public void Una_moneda_vacia_se_rechaza()
    {
        Assert.Throws<AppException>(() => new Money(10, "   "));
        Assert.Throws<AppException>(() => new Money(10, null));
    }

    [Fact]
    public void Stripe_cobra_en_centimos_no_en_soles()
    {
        // 29.90 soles son 2990 centimos. Redondear mal aqui cobra de menos o de mas.
        Assert.Equal(2990, new Money(29.9, "pen").ToMinorUnits());
        Assert.Equal(5, new Money(0.05, "pen").ToMinorUnits());
    }

    [Fact]
    public void Un_pago_nace_pendiente_y_sin_fecha_de_cobro()
    {
        var payment = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), null, 29.9, "PEN", "card");

        Assert.Equal(PaymentStatus.pending, payment.Status);
        Assert.False(payment.IsPaid);
        Assert.Null(payment.PaidAt);
        Assert.Equal("pen", payment.Currency);
    }

    [Fact]
    public void Solo_al_completarse_se_sella_la_fecha_de_cobro()
    {
        var payment = Payment.Create(null, Guid.NewGuid(), null, 59.9, "pen", "card");

        payment.MarkProcessing("pi_test_123");
        Assert.Equal(PaymentStatus.processing, payment.Status);
        Assert.Null(payment.PaidAt);

        payment.MarkProcessed("pi_test_123");
        Assert.Equal(PaymentStatus.processed, payment.Status);
        Assert.True(payment.IsPaid);
        Assert.NotNull(payment.PaidAt);
        Assert.Equal("pi_test_123", payment.StripePaymentIntentId);
    }

    [Fact]
    public void Un_pago_fallido_no_cuenta_como_pagado()
    {
        var payment = Payment.Create(null, Guid.NewGuid(), null, 10, "pen", "card");

        payment.MarkFailed("pi_test_fail");

        Assert.Equal(PaymentStatus.failed, payment.Status);
        Assert.False(payment.IsPaid);
        Assert.Null(payment.PaidAt);
    }

    [Fact]
    public void El_evento_del_webhook_nace_sin_procesar()
    {
        // Es lo que permite detectar reenvios: Stripe reintenta los webhooks y sin
        // este registro un mismo cobro se contabilizaria dos veces.
        var evt = PaymentWebhookEvent.Received(PaymentWebhookEvent.ProviderStripe,
            "evt_test_123", "checkout.session.completed", "{}");

        Assert.False(evt.Processed);
        Assert.Null(evt.ProcessedAt);

        evt.MarkProcessed();

        Assert.True(evt.Processed);
        Assert.NotNull(evt.ProcessedAt);
    }

    [Fact]
    public void El_comprobante_lleva_la_fecha_en_su_numero()
    {
        var invoice = Invoice.IssueFor(Guid.NewGuid(), 29.9, null);

        Assert.StartsWith($"INV-{DateTime.UtcNow:yyyyMMdd}-", invoice.InvoiceNumber);
        Assert.Equal(29.9, invoice.TotalAmount);
    }
}
