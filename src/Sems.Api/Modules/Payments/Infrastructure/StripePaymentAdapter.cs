using Sems.Api.Modules.Payments.Domain.Services;
using Sems.Api.Shared.Errors;
using Stripe;
using Stripe.Checkout;

namespace Sems.Api.Modules.Payments.Infrastructure;

/// <summary>
/// Adaptador de Stripe.
///
/// <para>Es el unico punto del sistema que conoce el SDK de Stripe. Traduce
/// entre sus tipos y los del puerto <see cref="IPaymentProvider"/>, de modo que
/// ni el dominio ni la capa de aplicacion dependen del proveedor.</para>
/// </summary>
public sealed class StripePaymentAdapter : IPaymentProvider
{
    private readonly string _secretKey;
    private readonly string _webhookSecret;
    private readonly string _defaultCurrency;
    private readonly ILogger<StripePaymentAdapter> _logger;

    public StripePaymentAdapter(IConfiguration configuration, ILogger<StripePaymentAdapter> logger)
    {
        _logger = logger;
        _secretKey = configuration["Stripe:SecretKey"]
                     ?? Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? string.Empty;
        _webhookSecret = configuration["Stripe:WebhookSecret"]
                         ?? Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET") ?? string.Empty;
        _defaultCurrency = configuration["Stripe:Currency"]
                           ?? Environment.GetEnvironmentVariable("STRIPE_CURRENCY") ?? "pen";

        if (string.IsNullOrWhiteSpace(_secretKey))
        {
            _logger.LogWarning("STRIPE_SECRET_KEY no esta configurada; los cobros fallaran");
        }
        else
        {
            StripeConfiguration.ApiKey = _secretKey;
        }
    }

    public async Task<PaymentMethodDetails> GetPaymentMethodDetailsAsync(string stripePaymentMethodId,
        CancellationToken ct = default)
    {
        try
        {
            var service = new PaymentMethodService();
            var method = await service.GetAsync(stripePaymentMethodId, cancellationToken: ct);
            var card = method.Card;

            return card is null
                ? new PaymentMethodDetails(method.Type, string.Empty, string.Empty, 0, 0)
                : new PaymentMethodDetails(method.Type, card.Brand, card.Last4,
                    (int)card.ExpMonth, (int)card.ExpYear);
        }
        catch (StripeException ex)
        {
            throw ProviderFailure("no se pudo leer el medio de pago", ex);
        }
    }

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(
        CreatePaymentIntentRequest request, CancellationToken ct = default)
    {
        try
        {
            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)Math.Round(request.Amount * 100),
                Currency = CurrencyOrDefault(request.Currency),
                PaymentMethod = request.StripePaymentMethodId,
                Confirm = true,
                // Sin esto, una tarjeta que pide autenticacion deja el cobro
                // colgado esperando una redireccion que nadie va a completar.
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                    AllowRedirects = "never"
                },
                Metadata = new Dictionary<string, string>
                {
                    ["user_id"] = request.UserId ?? string.Empty,
                    ["subscription_id"] = request.SubscriptionId ?? string.Empty,
                    ["payment_id"] = request.PaymentId ?? string.Empty
                }
            };

            var intent = await new PaymentIntentService().CreateAsync(options, cancellationToken: ct);
            return new PaymentIntentResult(intent.Id, intent.Status);
        }
        catch (StripeException ex)
        {
            throw ProviderFailure("no se pudo iniciar el cobro", ex);
        }
    }

    public async Task<PaymentIntentResult> ConfirmPaymentIntentAsync(string paymentIntentId,
        CancellationToken ct = default)
    {
        try
        {
            var intent = await new PaymentIntentService().GetAsync(paymentIntentId, cancellationToken: ct);
            return new PaymentIntentResult(intent.Id, intent.Status);
        }
        catch (StripeException ex)
        {
            throw ProviderFailure("no se pudo confirmar el cobro", ex);
        }
    }

    /// <summary>
    /// Crea una sesion de Stripe Checkout.
    ///
    /// <para>El usuario paga en una pagina alojada por Stripe, asi que los datos
    /// de la tarjeta nunca pasan por nuestros servidores. Los identificadores
    /// viajan como metadatos para poder correlacionar el webhook con nuestro
    /// registro.</para>
    /// </summary>
    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        CreateCheckoutSessionRequest request, CancellationToken ct = default)
    {
        try
        {
            var options = new SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = request.SuccessUrl,
                CancelUrl = request.CancelUrl,
                LineItems = new List<SessionLineItemOptions>
                {
                    new()
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = CurrencyOrDefault(request.Currency),
                            UnitAmount = (long)Math.Round(request.Amount * 100),
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = string.IsNullOrWhiteSpace(request.PlanName)
                                    ? "Suscripcion SEMS" : request.PlanName
                            }
                        }
                    }
                },
                Metadata = new Dictionary<string, string>
                {
                    ["user_id"] = request.UserId ?? string.Empty,
                    ["subscription_id"] = request.SubscriptionId ?? string.Empty
                }
            };

            var session = await new SessionService().CreateAsync(options, cancellationToken: ct);
            return new CheckoutSessionResult(session.Id, session.Url);
        }
        catch (StripeException ex)
        {
            throw ProviderFailure("no se pudo crear la sesion de pago", ex);
        }
    }

    public ProviderWebhookEvent ParseWebhookEvent(string payload, string signature)
    {
        if (string.IsNullOrWhiteSpace(_webhookSecret))
        {
            throw AppException.Internal("stripe webhook is not configured");
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signature, _webhookSecret);
        }
        catch (StripeException)
        {
            // Firma invalida: alguien esta enviando eventos que no vienen de Stripe.
            throw AppException.Validation("invalid stripe signature");
        }

        var data = stripeEvent.Data.Object;

        if (data is Session session)
        {
            var metadata = session.Metadata ?? new Dictionary<string, string>();
            var amount = session.AmountTotal is null ? 0.0 : session.AmountTotal.Value / 100.0;

            return new ProviderWebhookEvent(stripeEvent.Id, stripeEvent.Type, payload,
                session.PaymentIntentId, "succeeded", session.Id,
                metadata.GetValueOrDefault("user_id"),
                metadata.GetValueOrDefault("subscription_id"), amount, session.Currency);
        }

        if (data is PaymentIntent intent)
        {
            return new ProviderWebhookEvent(stripeEvent.Id, stripeEvent.Type, payload,
                intent.Id, intent.Status, null, null, null, null, null);
        }

        return new ProviderWebhookEvent(stripeEvent.Id, stripeEvent.Type, payload,
            null, null, null, null, null, null, null);
    }

    private string CurrencyOrDefault(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? _defaultCurrency : currency.Trim().ToLowerInvariant();

    private AppException ProviderFailure(string message, StripeException ex)
    {
        _logger.LogError("Stripe: {Message} ({Error})", message, ex.Message);
        return AppException.Internal($"external provider error: {message}");
    }
}
