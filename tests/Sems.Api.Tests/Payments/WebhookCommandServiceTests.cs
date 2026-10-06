using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sems.Api.Modules.Payments.Application;
using Sems.Api.Modules.Payments.Domain.Model;
using Sems.Api.Modules.Payments.Domain.Repositories;
using Sems.Api.Modules.Payments.Domain.Services;
using Sems.Api.Shared.Errors;
using Sems.Api.Shared.Events;
using Xunit;

namespace Sems.Api.Tests.Payments;

/// <summary>
/// Procesamiento de los avisos de Stripe con el puerto
/// <see cref="IPaymentProvider"/> simulado.
/// </summary>
public class WebhookCommandServiceTests
{
    private const string Payload = "{\"id\":\"evt_test_1\"}";
    private const string Signature = "t=1,v1=abc";

    private readonly IWebhookEventRepository _events = Substitute.For<IWebhookEventRepository>();
    private readonly IPaymentRepository _payments = Substitute.For<IPaymentRepository>();
    private readonly IInvoiceRepository _invoices = Substitute.For<IInvoiceRepository>();
    private readonly IPaymentProvider _provider = Substitute.For<IPaymentProvider>();
    private readonly IDomainEventBus _bus = Substitute.For<IDomainEventBus>();
    private readonly WebhookCommandService _service;

    public WebhookCommandServiceTests()
    {
        _events.SaveAsync(Arg.Any<PaymentWebhookEvent>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<PaymentWebhookEvent>());
        _payments.SaveAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Payment>());
        _invoices.SaveAsync(Arg.Any<Invoice>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Invoice>());

        var mapper = new PaymentStatusMapper();
        var paymentCommands = new PaymentCommandService(_payments,
            Substitute.For<IPaymentMethodRepository>(), _invoices, _provider, mapper, _bus,
            NullLogger<PaymentCommandService>.Instance);

        _service = new WebhookCommandService(_events, _payments, _invoices, _provider, mapper,
            paymentCommands, NullLogger<WebhookCommandService>.Instance);
    }

    private static ProviderWebhookEvent CheckoutCompleted(Guid userId) => new(
        "evt_test_1", "checkout.session.completed", Payload, "pi_test_1", "succeeded", "cs_test_1",
        userId.ToString(), Guid.NewGuid().ToString(), 79.90, "pen");

    [Fact]
    public async Task HandleStripeAsync_NewCheckoutCompleted_ChargesThePaymentIssuesTheInvoiceAndPublishesPaymentProcessed()
    {
        var userId = Guid.NewGuid();
        _provider.ParseWebhookEvent(Payload, Signature).Returns(CheckoutCompleted(userId));

        var processed = await _service.HandleStripeAsync(Payload, Signature);

        Assert.True(processed);
        await _payments.Received(1).SaveAsync(
            Arg.Is<Payment>(p => p.IsPaid && p.Amount == 79.90 && p.UserId == userId
                                 && p.StripePaymentIntentId == "pi_test_1"),
            Arg.Any<CancellationToken>());
        await _invoices.Received(1).SaveAsync(Arg.Is<Invoice>(i => i.TotalAmount == 79.90),
            Arg.Any<CancellationToken>());
        _bus.Received(1).Publish(Arg.Is<DomainEvents.PaymentProcessed>(e =>
            e.UserId == userId && e.Amount == 79.90m && e.Status == "processed"));
        await _events.Received().SaveAsync(Arg.Is<PaymentWebhookEvent>(e => e.Processed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleStripeAsync_EventAlreadyReceived_IsDiscardedWithoutChargingOrSaving()
    {
        _provider.ParseWebhookEvent(Payload, Signature).Returns(CheckoutCompleted(Guid.NewGuid()));
        _events.FindByProviderEventIdAsync("evt_test_1", Arg.Any<CancellationToken>())
            .Returns(PaymentWebhookEvent.Received(PaymentWebhookEvent.ProviderStripe, "evt_test_1",
                "checkout.session.completed", Payload));

        var processed = await _service.HandleStripeAsync(Payload, Signature);

        Assert.False(processed);
        await _payments.DidNotReceive().SaveAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
        await _events.DidNotReceive().SaveAsync(Arg.Any<PaymentWebhookEvent>(), Arg.Any<CancellationToken>());
        _bus.DidNotReceive().Publish(Arg.Any<IDomainEvent>());
    }

    [Fact]
    public async Task HandleStripeAsync_InvalidSignature_ThrowsValidationErrorAndSavesNothing()
    {
        _provider.ParseWebhookEvent(Payload, Signature)
            .Throws(AppException.Validation("invalid stripe signature"));

        var error = await Assert.ThrowsAsync<AppException>(
            () => _service.HandleStripeAsync(Payload, Signature));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
        await _events.DidNotReceive().SaveAsync(Arg.Any<PaymentWebhookEvent>(), Arg.Any<CancellationToken>());
        await _payments.DidNotReceive().SaveAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleStripeAsync_UnknownStripeStatus_IsNeverTreatedAsPaid()
    {
        var payment = Payment.Create(null, Guid.NewGuid(), null, 79.90, "pen", "card");
        payment.MarkProcessing("pi_test_2");
        _payments.FindByStripePaymentIntentIdAsync("pi_test_2", Arg.Any<CancellationToken>())
            .Returns(payment);
        _provider.ParseWebhookEvent(Payload, Signature).Returns(new ProviderWebhookEvent(
            "evt_test_2", "payment_intent.processing", Payload, "pi_test_2", "some_new_status",
            null, null, null, null, null));

        await _service.HandleStripeAsync(Payload, Signature);

        Assert.Equal(PaymentStatus.processing, payment.Status);
        Assert.False(payment.IsPaid);
        Assert.Null(payment.PaidAt);
        await _invoices.DidNotReceive().SaveAsync(Arg.Any<Invoice>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("succeeded", PaymentStatus.processed)]
    [InlineData("requires_payment_method", PaymentStatus.failed)]
    [InlineData("canceled", PaymentStatus.cancelled)]
    [InlineData("requires_action", PaymentStatus.processing)]
    [InlineData("something_new", PaymentStatus.processing)]
    [InlineData(null, PaymentStatus.processing)]
    public void FromStripe_StripeStatus_MapsToTheDomainStatus(string? stripeStatus, PaymentStatus expected)
    {
        Assert.Equal(expected, new PaymentStatusMapper().FromStripe(stripeStatus));
    }
}
