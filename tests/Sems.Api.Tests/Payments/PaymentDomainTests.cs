using Sems.Api.Modules.Payments.Domain.Model;
using Sems.Api.Shared.Errors;
using Xunit;

namespace Sems.Api.Tests.Payments;

/// <summary>
/// <see cref="Money"/>, <see cref="Payment"/>, <see cref="Invoice"/> y
/// <see cref="PaymentWebhookEvent"/>: las reglas que no dependen de Stripe ni de
/// la base de datos.
///
/// <para>Son las que mas caro cuestan si se rompen: un importe negativo aceptado
/// o un cobro que salta de pendiente a pagado sin pasar por el proveedor son
/// errores de dinero, no de pantalla.</para>
/// </summary>
public class PaymentDomainTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Money_NonPositiveAmount_ThrowsValidationError(double amount)
    {
        Assert.Equal(ErrorCode.VALIDATION_ERROR,
            Assert.Throws<AppException>(() => new Money(amount, "pen")).Code);
    }

    [Fact]
    public void Money_Currency_IsNormalizedToLowercaseForStripe()
    {
        var money = new Money(29.9, "  PEN  ");

        Assert.Equal("pen", money.Currency);
        Assert.Equal(29.9, money.Amount);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(null)]
    public void Money_EmptyCurrency_ThrowsValidationError(string? currency)
    {
        Assert.Equal(ErrorCode.VALIDATION_ERROR,
            Assert.Throws<AppException>(() => new Money(10, currency)).Code);
    }

    [Theory]
    [InlineData(29.90, 2990)]
    [InlineData(79.90, 7990)]
    [InlineData(0.05, 5)]
    public void ToMinorUnits_AmountInSoles_ReturnsCentsForStripe(double amount, long cents)
    {
        // 29.90 soles son 2990 centimos. Redondear mal aqui cobra de menos o de mas.
        Assert.Equal(cents, new Money(amount, "pen").ToMinorUnits());
    }

    [Fact]
    public void Create_NewPayment_StartsPendingWithoutPaymentDate()
    {
        var payment = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), null, 29.9, "PEN", "card");

        Assert.Equal(PaymentStatus.pending, payment.Status);
        Assert.False(payment.IsPaid);
        Assert.Null(payment.PaidAt);
        Assert.Equal("pen", payment.Currency);
    }

    [Fact]
    public void MarkProcessed_AfterProcessing_OnlyThenStampsThePaymentDate()
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

    [Theory]
    [InlineData(PaymentStatus.failed)]
    [InlineData(PaymentStatus.cancelled)]
    public void MarkFailedOrCancelled_Payment_DoesNotCountAsPaid(PaymentStatus outcome)
    {
        var payment = Payment.Create(null, Guid.NewGuid(), null, 10, "pen", "card");

        if (outcome == PaymentStatus.failed)
        {
            payment.MarkFailed("pi_test_fail");
        }
        else
        {
            payment.MarkCancelled("pi_test_fail");
        }

        Assert.Equal(outcome, payment.Status);
        Assert.False(payment.IsPaid);
        Assert.Null(payment.PaidAt);
    }

    [Fact]
    public void Create_InvalidAmount_ThrowsValidationErrorBeforeCreatingThePayment()
    {
        Assert.Equal(ErrorCode.VALIDATION_ERROR, Assert.Throws<AppException>(() =>
            Payment.Create(null, Guid.NewGuid(), null, 0, "pen", "card")).Code);
    }

    [Fact]
    public void Received_WebhookEvent_StartsUnprocessedUntilMarked()
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
    public void IssueFor_Payment_CarriesTheDateInItsNumber()
    {
        var invoice = Invoice.IssueFor(Guid.NewGuid(), 29.9, null);

        Assert.StartsWith($"INV-{DateTime.UtcNow:yyyyMMdd}-", invoice.InvoiceNumber);
        Assert.Equal(29.9, invoice.TotalAmount);
    }
}
