using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Sems.Api.Modules.Subscriptions.Domain.Model;

namespace Sems.Api.Modules.Subscriptions.Interfaces;

/// <summary>
/// Contrato JSON del modulo de suscripciones.
///
/// <para><b>Es un contrato mixto y hay que respetarlo tal cual.</b> El servicio
/// en Go leia los cuerpos con etiquetas snake_case, pero devolvia las entidades
/// directamente, sin etiquetas, de modo que Go serializaba usando el nombre del
/// campo en PascalCase. El frontend esta escrito contra eso y lo documenta en
/// <c>subscriptions.service.ts</c>.</para>
///
/// <para>Por eso: <b>peticiones en snake_case, respuestas en PascalCase</b>.</para>
/// </summary>
public static class SubscriptionResources
{
    // ----------------------------------------------- peticiones (snake_case)

    public sealed record CreateSubscriptionRequest(
        [property: JsonPropertyName("user_id")]
        [property: Required(ErrorMessage = "is required")] string UserId,
        [property: JsonPropertyName("plan_id")]
        [property: Required(ErrorMessage = "is required")] string PlanId,
        [property: JsonPropertyName("stripe_customer_id")] string? StripeCustomerId);

    public sealed record ChangePlanRequest(
        [property: JsonPropertyName("new_plan_id")]
        [property: Required(ErrorMessage = "is required")] string NewPlanId);

    // ------------------------------------------- respuestas (PascalCase)

    public sealed record PlanFeatureResource(
        [property: JsonPropertyName("FeatureID")] string FeatureId,
        [property: JsonPropertyName("PlanID")] string PlanId,
        [property: JsonPropertyName("FeatureCode")] string? FeatureCode,
        [property: JsonPropertyName("FeatureName")] string? FeatureName,
        [property: JsonPropertyName("FeatureValue")] string? FeatureValue,
        [property: JsonPropertyName("CreatedAt")] DateTime CreatedAt)
    {
        public static PlanFeatureResource From(PlanFeature f) => new(f.FeatureId.ToString(),
            f.PlanId.ToString(), f.FeatureCode, f.FeatureName, f.FeatureValue, f.CreatedAt);
    }

    public sealed record PlanResource(
        [property: JsonPropertyName("PlanID")] string PlanId,
        [property: JsonPropertyName("Name")] string Name,
        [property: JsonPropertyName("Description")] string? Description,
        [property: JsonPropertyName("Price")] double Price,
        [property: JsonPropertyName("Currency")] string Currency,
        [property: JsonPropertyName("BillingPeriod")] string BillingPeriod,
        [property: JsonPropertyName("Active")] bool Active,
        [property: JsonPropertyName("CreatedAt")] DateTime CreatedAt,
        [property: JsonPropertyName("PlanFeatures")] List<PlanFeatureResource> PlanFeatures)
    {
        public static PlanResource From(SubscriptionPlan p) => new(p.PlanId.ToString(), p.Name,
            p.Description, p.Price, p.Currency, p.BillingPeriod, p.Active, p.CreatedAt,
            p.PlanFeatures.Select(PlanFeatureResource.From).ToList());
    }

    public sealed record SubscriptionResource(
        [property: JsonPropertyName("SubscriptionID")] string SubscriptionId,
        [property: JsonPropertyName("UserID")] string UserId,
        [property: JsonPropertyName("PlanID")] string PlanId,
        [property: JsonPropertyName("Status")] string Status,
        [property: JsonPropertyName("StartDate")] DateTime StartDate,
        [property: JsonPropertyName("EndDate")] DateTime? EndDate,
        [property: JsonPropertyName("StripeSubscriptionID")] string? StripeSubscriptionId,
        [property: JsonPropertyName("CreatedAt")] DateTime CreatedAt)
    {
        public static SubscriptionResource From(Subscription s) => new(s.SubscriptionId.ToString(),
            s.UserId, s.PlanId.ToString(), s.Status.ToString(), s.StartDate, s.EndDate,
            s.StripeSubscriptionId, s.CreatedAt);
    }
}
