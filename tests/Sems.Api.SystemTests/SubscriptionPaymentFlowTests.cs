using System.Net;
using Sems.Api.TestSupport;

namespace Sems.Api.SystemTests;

/// <summary>
/// Flujo 3: contratacion y pago de un plan (US40, US41, US44, TS08).
/// </summary>
/// <remarks>
/// Encadena Subscriptions, el webhook firmado de Payments y el correo de
/// comprobante que dispara <c>PaymentProcessed</c>.
/// </remarks>
public class SubscriptionPaymentFlowTests : IClassFixture<SemsApiFactory>
{
    private const string ReceiptSubject = "SEMS payment receipt";

    private readonly SemsApiFactory _factory;

    public SubscriptionPaymentFlowTests(SemsApiFactory factory) => _factory = factory;

    [Fact]
    public async Task SubscriptionPayment_ProPlanPaidThroughCheckout_LeavesOneProcessedPaymentInvoiceAndReceipt()
    {
        var user = await _factory.CreateUserAsync();
        var webhook = _factory.CreateClient();

        // US40: consulta los planes.
        var plans = await (await user.Client.GetAsync("/api/v1/subscription-plans"))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.Equal(3, plans.GetArrayLength());
        var pro = plans.EnumerateArray().Single(p => p.Text("Name") == "Pro");
        Assert.Equal(79.90m, pro.Number("Price"));

        // US41: se suscribe al plan Pro.
        var subscription = await (await user.Client.PostJsonAsync("/api/v1/subscriptions",
                new { user_id = user.UserId.ToString(), plan_id = pro.Text("PlanID") }))
            .ExpectAsync(HttpStatusCode.Created);
        var subscriptionId = Guid.Parse(subscription.Text("SubscriptionID"));

        // TS08: Stripe confirma el checkout con un evento firmado...
        var payload = StripeEventSigner.CheckoutSessionCompleted($"evt_test_{Guid.NewGuid():N}",
            user.UserId, subscriptionId, amountTotalCents: 7990);
        var first = await (await webhook.SendAsync(
                StripeEventSigner.WebhookRequest(payload, StripeEventSigner.Sign(payload))))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.True(first.GetProperty("processed").GetBoolean());

        // ... y lo reenvia: el duplicado se descarta.
        var resent = await (await webhook.SendAsync(
                StripeEventSigner.WebhookRequest(payload, StripeEventSigner.Sign(payload))))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.False(resent.GetProperty("processed").GetBoolean());

        // Un unico pago procesado de S/ 79.90, asociado a la suscripcion.
        var payments = await (await user.Client.GetAsync($"/api/v1/payments/user/{user.UserId}"))
            .ExpectAsync(HttpStatusCode.OK);
        var payment = Assert.Single(payments.EnumerateArray());
        Assert.Equal("processed", payment.Text("status"));
        Assert.Equal(79.90m, payment.Number("amount"));
        Assert.Equal(subscriptionId.ToString(), payment.Text("subscription_id"));

        // US44: su comprobante.
        var invoice = await (await user.Client.GetAsync($"/api/v1/invoices/payment/{payment.Text("payment_id")}"))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.Equal(79.90m, invoice.Number("total_amount"));
        Assert.StartsWith("INV-", invoice.Text("invoice_number"));

        // Y un unico correo de comprobante.
        var receipt = Assert.Single(_factory.Emails.SentTo(user.Email, ReceiptSubject));
        Assert.Contains(payment.Text("payment_id"), receipt.Body);
    }
}
