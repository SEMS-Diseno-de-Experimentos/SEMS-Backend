using Sems.Api.Modules.Payments.Domain.Model;
using Sems.Api.Modules.Payments.Domain.Repositories;
using Sems.Api.Modules.Payments.Domain.Services;

namespace Sems.Api.Modules.Payments.Application;

/// <summary>
/// Procesa los avisos que envia Stripe.
///
/// <para>El webhook es la unica fuente fiable sobre si el dinero se movio: la
/// respuesta inmediata al usuario puede perderse si cierra el navegador, pero
/// Stripe reintenta el aviso hasta que respondemos 200.</para>
/// </summary>
public sealed class WebhookCommandService
{
    private readonly IWebhookEventRepository _events;
    private readonly IPaymentRepository _payments;
    private readonly IInvoiceRepository _invoices;
    private readonly IPaymentProvider _provider;
    private readonly PaymentStatusMapper _statusMapper;
    private readonly PaymentCommandService _paymentCommands;
    private readonly ILogger<WebhookCommandService> _logger;

    public WebhookCommandService(IWebhookEventRepository events, IPaymentRepository payments,
        IInvoiceRepository invoices, IPaymentProvider provider, PaymentStatusMapper statusMapper,
        PaymentCommandService paymentCommands, ILogger<WebhookCommandService> logger)
    {
        _events = events;
        _payments = payments;
        _invoices = invoices;
        _provider = provider;
        _statusMapper = statusMapper;
        _paymentCommands = paymentCommands;
        _logger = logger;
    }

    /// <returns>true si el evento se proceso, false si era un reenvio ya conocido</returns>
    public async Task<bool> HandleStripeAsync(string payload, string signature,
        CancellationToken ct = default)
    {
        var evt = _provider.ParseWebhookEvent(payload, signature);

        // Stripe reintenta: un evento ya visto se descarta sin volver a cobrar.
        if (await _events.FindByProviderEventIdAsync(evt.ProviderEventId, ct) is not null)
        {
            _logger.LogInformation("Webhook duplicado descartado: {EventId}", evt.ProviderEventId);
            return false;
        }

        var stored = await _events.SaveAsync(PaymentWebhookEvent.Received(
            PaymentWebhookEvent.ProviderStripe, evt.ProviderEventId, evt.EventType, evt.Payload), ct);

        switch (evt.EventType)
        {
            case "checkout.session.completed":
                await HandleCheckoutCompletedAsync(evt, ct);
                break;
            case "payment_intent.succeeded":
            case "payment_intent.payment_failed":
            case "payment_intent.canceled":
            case "payment_intent.processing":
                await HandlePaymentIntentAsync(evt, ct);
                break;
            default:
                _logger.LogDebug("Evento de Stripe sin manejador: {Type}", evt.EventType);
                break;
        }

        stored.MarkProcessed();
        await _events.SaveAsync(stored, ct);
        return true;
    }

    /// <summary>El usuario completo el pago en la pagina alojada por Stripe.</summary>
    private async Task HandleCheckoutCompletedAsync(ProviderWebhookEvent evt, CancellationToken ct)
    {
        if (!Guid.TryParse(evt.CheckoutUserId, out var userId))
        {
            _logger.LogWarning("checkout.session.completed sin user_id en los metadatos; se ignora");
            return;
        }

        Guid? subscriptionId = Guid.TryParse(evt.CheckoutSubscriptionId, out var sub) ? sub : null;

        await _paymentCommands.RecordCheckoutPaymentAsync(userId, subscriptionId,
            evt.CheckoutAmount ?? 0.0, evt.CheckoutCurrency, evt.StripePaymentIntentId, ct);
    }

    /// <summary>Cambio de estado de un cobro iniciado desde la aplicacion.</summary>
    private async Task HandlePaymentIntentAsync(ProviderWebhookEvent evt, CancellationToken ct)
    {
        var payment = await _payments.FindByStripePaymentIntentIdAsync(evt.StripePaymentIntentId, ct);
        if (payment is null)
        {
            _logger.LogWarning("Webhook para un intent desconocido: {Intent}",
                evt.StripePaymentIntentId);
            return;
        }

        switch (_statusMapper.FromStripe(evt.PaymentStatus))
        {
            case PaymentStatus.processed: payment.MarkProcessed(evt.StripePaymentIntentId); break;
            case PaymentStatus.failed: payment.MarkFailed(evt.StripePaymentIntentId); break;
            case PaymentStatus.cancelled: payment.MarkCancelled(evt.StripePaymentIntentId); break;
            default: payment.MarkProcessing(evt.StripePaymentIntentId); break;
        }

        var saved = await _payments.SaveAsync(payment, ct);

        if (saved.IsPaid && await _invoices.FindByPaymentIdAsync(saved.PaymentId, ct) is null)
        {
            await _invoices.SaveAsync(Invoice.IssueFor(saved.PaymentId, saved.Amount, null), ct);
        }
    }
}
