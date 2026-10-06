using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Stripe;

namespace Sems.Api.TestSupport;

/// <summary>
/// Construye y firma eventos de Stripe igual que lo hace Stripe.
/// </summary>
/// <remarks>
/// La API verifica la firma con el SDK real de Stripe; las pruebas no la
/// desactivan. Por eso el evento se firma con el secreto de prueba usando el
/// mismo esquema que Stripe: <c>t=&lt;unix&gt;,v1=HMAC-SHA256(secreto, "t.cuerpo")</c>.
/// </remarks>
public static class StripeEventSigner
{
    public const string SignatureHeader = "Stripe-Signature";

    /// <summary>Cabecera <c>Stripe-Signature</c> para un cuerpo dado.</summary>
    public static string Sign(string payload, string secret = SemsApiFactory.StripeWebhookSecret,
        DateTimeOffset? at = null)
    {
        var timestamp = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}"))).ToLowerInvariant();
        return $"t={timestamp},v1={signature}";
    }

    /// <summary>
    /// Evento <c>checkout.session.completed</c>: el usuario pago en la pagina de
    /// Stripe. El usuario y la suscripcion viajan en los metadatos, como los
    /// envia la API al abrir la sesion.
    /// </summary>
    public static string CheckoutSessionCompleted(string eventId, Guid userId,
        Guid? subscriptionId, long amountTotalCents, string currency = "pen",
        string? paymentIntentId = null)
    {
        var evt = new Dictionary<string, object?>
        {
            ["id"] = eventId,
            ["object"] = "event",
            // La version del SDK: ConstructEvent rechaza eventos de otra version.
            ["api_version"] = StripeConfiguration.ApiVersion,
            ["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["livemode"] = false,
            ["pending_webhooks"] = 1,
            ["request"] = new Dictionary<string, object?> { ["id"] = null, ["idempotency_key"] = null },
            ["type"] = "checkout.session.completed",
            ["data"] = new Dictionary<string, object?>
            {
                ["object"] = new Dictionary<string, object?>
                {
                    ["id"] = $"cs_test_{Guid.NewGuid():N}",
                    ["object"] = "checkout.session",
                    ["mode"] = "payment",
                    ["status"] = "complete",
                    ["payment_status"] = "paid",
                    ["amount_total"] = amountTotalCents,
                    ["currency"] = currency,
                    ["payment_intent"] = paymentIntentId ?? $"pi_test_{Guid.NewGuid():N}",
                    ["metadata"] = new Dictionary<string, string>
                    {
                        ["user_id"] = userId.ToString(),
                        ["subscription_id"] = subscriptionId?.ToString() ?? string.Empty
                    }
                }
            }
        };

        return JsonSerializer.Serialize(evt);
    }

    /// <summary>Peticion al webhook con el cuerpo y la firma indicados.</summary>
    public static HttpRequestMessage WebhookRequest(string payload, string? signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        if (signature is not null)
        {
            request.Headers.TryAddWithoutValidation(SignatureHeader, signature);
        }
        return request;
    }
}
