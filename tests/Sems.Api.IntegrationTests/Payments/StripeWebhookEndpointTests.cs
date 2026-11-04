using System.Net;
using Microsoft.EntityFrameworkCore;
using Sems.Api.Modules.Payments.Domain.Model;
using Sems.Api.TestSupport;

namespace Sems.Api.IntegrationTests.Payments;

/// <summary>
/// Webhook de Stripe. Los eventos se firman con el secreto de prueba: la
/// verificacion real del SDK de Stripe no se desactiva.
/// </summary>
public class StripeWebhookEndpointTests : IClassFixture<SemsApiFactory>
{
    private readonly SemsApiFactory _factory;
    private readonly HttpClient _client;

    public StripeWebhookEndpointTests(SemsApiFactory factory)
    {
        _factory = factory;
        // El webhook no lleva sesion: se autentica por la firma del cuerpo.
        _client = factory.CreateClient();
    }

    private static string NewEventId() => $"evt_test_{Guid.NewGuid():N}";

    private Task<List<Payment>> PaymentsOfAsync(Guid userId) =>
        _factory.QueryDatabaseAsync(db => db.Set<Payment>().Where(p => p.UserId == userId).ToListAsync());

    [Fact]
    public async Task HandleStripe_WithoutSignatureHeader_Returns400()
    {
        var payload = StripeEventSigner.CheckoutSessionCompleted(NewEventId(), Guid.NewGuid(), null, 7990);

        var response = await _client.SendAsync(StripeEventSigner.WebhookRequest(payload, signature: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HandleStripe_SignedWithAnotherSecret_Returns400AndRegistersNoPayment()
    {
        var user = await _factory.CreateUserAsync();
        var payload = StripeEventSigner.CheckoutSessionCompleted(NewEventId(), user.UserId, null, 7990);
        var signature = StripeEventSigner.Sign(payload, "whsec_someone_else");

        var response = await _client.SendAsync(StripeEventSigner.WebhookRequest(payload, signature));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await PaymentsOfAsync(user.UserId));
    }

    [Fact]
    public async Task HandleStripe_TamperedBody_Returns400AndRegistersNoPayment()
    {
        var user = await _factory.CreateUserAsync();
        var payload = StripeEventSigner.CheckoutSessionCompleted(NewEventId(), user.UserId, null, 7990);
        var signature = StripeEventSigner.Sign(payload);
        var tampered = payload.Replace("7990", "1", StringComparison.Ordinal);

        var response = await _client.SendAsync(StripeEventSigner.WebhookRequest(tampered, signature));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await PaymentsOfAsync(user.UserId));
    }

    [Fact]
    public async Task HandleStripe_ValidSignature_Returns200WithProcessedPaymentAndInvoice()
    {
        var user = await _factory.CreateUserAsync();
        var payload = StripeEventSigner.CheckoutSessionCompleted(NewEventId(), user.UserId, null, 7990);

        var response = await _client.SendAsync(
            StripeEventSigner.WebhookRequest(payload, StripeEventSigner.Sign(payload)));

        var body = await response.ExpectAsync(HttpStatusCode.OK);
        Assert.True(body.GetProperty("processed").GetBoolean());

        var payment = Assert.Single(await PaymentsOfAsync(user.UserId));
        Assert.Equal(PaymentStatus.processed, payment.Status);
        Assert.Equal(79.90, payment.Amount);
        var invoice = await (await user.Client.GetAsync($"/api/v1/invoices/payment/{payment.PaymentId}"))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.StartsWith("INV-", invoice.Text("invoice_number"));
    }

    [Fact]
    public async Task HandleStripe_SameEventTwice_BothReturn200ButOnlyTheFirstIsProcessed()
    {
        var user = await _factory.CreateUserAsync();
        var payload = StripeEventSigner.CheckoutSessionCompleted(NewEventId(), user.UserId, null, 7990);

        var first = await _client.SendAsync(
            StripeEventSigner.WebhookRequest(payload, StripeEventSigner.Sign(payload)));
        var second = await _client.SendAsync(
            StripeEventSigner.WebhookRequest(payload, StripeEventSigner.Sign(payload)));

        Assert.True((await first.ExpectAsync(HttpStatusCode.OK)).GetProperty("processed").GetBoolean());
        Assert.False((await second.ExpectAsync(HttpStatusCode.OK)).GetProperty("processed").GetBoolean());
        Assert.Single(await PaymentsOfAsync(user.UserId));
    }
}
