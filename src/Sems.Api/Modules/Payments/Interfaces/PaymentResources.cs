using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Sems.Api.Modules.Payments.Domain.Model;

namespace Sems.Api.Modules.Payments.Interfaces;

/// <summary>Contrato JSON del modulo de pagos, en snake_case como el original.</summary>
public static class PaymentResources
{
    // ------------------------------------------------------------- peticiones

    public sealed record ProcessPaymentRequest(
        [property: JsonPropertyName("subscription_id")]
        [property: Required(ErrorMessage = "is required")] string SubscriptionId,
        [property: JsonPropertyName("user_id")]
        [property: Required(ErrorMessage = "is required")] string UserId,
        [property: JsonPropertyName("payment_method_id")]
        [property: Required(ErrorMessage = "is required")] string PaymentMethodId,
        [property: JsonPropertyName("amount")] double Amount,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("payment_method")] string? PaymentMethod);

    public sealed record RegisterPaymentMethodRequest(
        [property: JsonPropertyName("user_id")]
        [property: Required(ErrorMessage = "is required")] string UserId,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("stripe_payment_method_id")]
        [property: Required(ErrorMessage = "is required")] string StripePaymentMethodId,
        [property: JsonPropertyName("is_default")] bool IsDefault);

    /// <summary>Cuerpo que envia la aplicacion web para abrir Stripe Checkout.</summary>
    public sealed record CreateCheckoutRequest(
        [property: JsonPropertyName("user_id")]
        [property: Required(ErrorMessage = "is required")] string UserId,
        [property: JsonPropertyName("subscription_id")] string? SubscriptionId,
        [property: JsonPropertyName("plan_name")] string? PlanName,
        [property: JsonPropertyName("amount")] double Amount,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("success_url")]
        [property: Required(ErrorMessage = "is required")] string SuccessUrl,
        [property: JsonPropertyName("cancel_url")]
        [property: Required(ErrorMessage = "is required")] string CancelUrl);

    // -------------------------------------------------------------- respuestas

    public sealed record PaymentResponse(
        [property: JsonPropertyName("payment_id")] string PaymentId,
        [property: JsonPropertyName("subscription_id")] string? SubscriptionId,
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("payment_method_id")] string? PaymentMethodId,
        [property: JsonPropertyName("amount")] double Amount,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("status")] PaymentStatus Status,
        [property: JsonPropertyName("payment_method")] string? PaymentMethod,
        [property: JsonPropertyName("stripe_payment_intent_id")] string? StripePaymentIntentId,
        [property: JsonPropertyName("paid_at")] DateTime? PaidAt,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt)
    {
        public static PaymentResponse From(Payment p) => new(p.PaymentId.ToString(),
            p.SubscriptionId?.ToString(), p.UserId.ToString(), p.PaymentMethodId?.ToString(),
            p.Amount, p.Currency, p.Status, p.PaymentMethod, p.StripePaymentIntentId, p.PaidAt,
            p.CreatedAt);
    }

    public sealed record InvoiceResponse(
        [property: JsonPropertyName("invoice_id")] string InvoiceId,
        [property: JsonPropertyName("payment_id")] string PaymentId,
        [property: JsonPropertyName("invoice_number")] string InvoiceNumber,
        [property: JsonPropertyName("issued_at")] DateTime IssuedAt,
        [property: JsonPropertyName("total_amount")] double TotalAmount,
        [property: JsonPropertyName("pdf_url")] string? PdfUrl)
    {
        public static InvoiceResponse From(Invoice i) => new(i.InvoiceId.ToString(),
            i.PaymentId.ToString(), i.InvoiceNumber, i.IssuedAt, i.TotalAmount, i.PdfUrl);
    }

    public sealed record ProcessPaymentResponse(
        [property: JsonPropertyName("payment")] PaymentResponse Payment,
        [property: JsonPropertyName("invoice")] InvoiceResponse? Invoice);

    public sealed record PaymentMethodResponse(
        [property: JsonPropertyName("payment_method_id")] string PaymentMethodId,
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("brand")] string? Brand,
        [property: JsonPropertyName("last4")] string? Last4,
        [property: JsonPropertyName("exp_month")] int ExpMonth,
        [property: JsonPropertyName("exp_year")] int ExpYear,
        [property: JsonPropertyName("stripe_payment_method_id")] string? StripePaymentMethodId,
        [property: JsonPropertyName("is_default")] bool IsDefault,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt)
    {
        public static PaymentMethodResponse From(PaymentMethodEntity m) =>
            new(m.PaymentMethodId.ToString(), m.UserId.ToString(), m.Type, m.Brand, m.Last4,
                m.ExpMonth, m.ExpYear, m.StripePaymentMethodId, m.IsDefault, m.CreatedAt);
    }

    public sealed record CheckoutSessionResponse(
        [property: JsonPropertyName("session_id")] string? SessionId,
        [property: JsonPropertyName("url")] string? Url);
}
