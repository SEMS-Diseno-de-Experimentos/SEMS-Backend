using Sems.Api.Modules.Payments.Domain.Model;

namespace Sems.Api.Modules.Payments.Domain.Services;

/// <summary>
/// Puerto hacia el procesador de pagos.
///
/// <para>La capa de aplicacion depende solo de esta interfaz, nunca del SDK de
/// Stripe. Eso permite sustituir el proveedor o usar un doble en pruebas sin
/// tocar la logica de negocio.</para>
/// </summary>
public interface IPaymentProvider
{
    Task<PaymentMethodDetails> GetPaymentMethodDetailsAsync(string stripePaymentMethodId,
        CancellationToken ct = default);

    Task<PaymentIntentResult> CreatePaymentIntentAsync(CreatePaymentIntentRequest request,
        CancellationToken ct = default);

    Task<PaymentIntentResult> ConfirmPaymentIntentAsync(string paymentIntentId,
        CancellationToken ct = default);

    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(CreateCheckoutSessionRequest request,
        CancellationToken ct = default);

    /// <summary>Verifica la firma y traduce el evento. Lanza si la firma no cuadra.</summary>
    ProviderWebhookEvent ParseWebhookEvent(string payload, string signature);
}

/// <summary>Datos visibles de una tarjeta guardada.</summary>
public sealed record PaymentMethodDetails(string? Type, string? Brand, string? Last4,
    int ExpMonth, int ExpYear);

public sealed record CreatePaymentIntentRequest(double Amount, string? Currency,
    string? StripePaymentMethodId, string? UserId, string? SubscriptionId, string? PaymentId);

public sealed record PaymentIntentResult(string? Id, string? Status);

/// <summary>Solicitud de sesion de pago alojada por Stripe (Stripe Checkout).</summary>
public sealed record CreateCheckoutSessionRequest(string? UserId, string? SubscriptionId,
    string? PlanName, double Amount, string? Currency, string SuccessUrl, string CancelUrl);

public sealed record CheckoutSessionResult(string? SessionId, string? Url);

/// <summary>Evento de webhook ya verificado y normalizado por el adaptador.</summary>
public sealed record ProviderWebhookEvent(string ProviderEventId, string EventType, string Payload,
    string? StripePaymentIntentId, string? PaymentStatus, string? CheckoutSessionId,
    string? CheckoutUserId, string? CheckoutSubscriptionId, double? CheckoutAmount,
    string? CheckoutCurrency);

/// <summary>
/// Traduce el vocabulario de estados de Stripe al del dominio.
///
/// <para>Aisla al dominio del proveedor: si manana cambian sus nombres de
/// estado, solo cambia esta clase.</para>
/// </summary>
public sealed class PaymentStatusMapper
{
    public PaymentStatus FromStripe(string? status) => status switch
    {
        "succeeded" => PaymentStatus.processed,
        "requires_payment_method" or "payment_failed" => PaymentStatus.failed,
        "canceled" or "cancelled" => PaymentStatus.cancelled,
        "processing" or "requires_confirmation" or "requires_action" => PaymentStatus.processing,
        // Un estado desconocido se trata como en curso, nunca como cobrado.
        _ => PaymentStatus.processing
    };
}
