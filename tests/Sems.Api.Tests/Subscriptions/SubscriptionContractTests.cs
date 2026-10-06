using System.Text.Json;
using Sems.Api.Modules.Subscriptions.Domain.Model;
using Sems.Api.Modules.Subscriptions.Interfaces;
using Sems.Api.Shared.Errors;
using Xunit;

namespace Sems.Api.Tests.Subscriptions;

/// <summary>
/// Suscripciones tiene el contrato mas fragil de todo el backend: <b>las
/// peticiones van en snake_case y las respuestas en PascalCase</b>.
///
/// <para>No es un descuido. El servicio original en Go leia los cuerpos con
/// etiquetas <c>json:"user_id"</c> pero devolvia las entidades sin etiquetar, de
/// modo que Go serializaba con el nombre del campo. El frontend se escribio
/// contra ese comportamiento y lo deja documentado en
/// <c>subscriptions.service.ts</c>.</para>
///
/// <para>Es exactamente el tipo de asimetria que alguien "arregla" al leerla.
/// Estas pruebas existen para que ese arreglo falle aqui y no en produccion.</para>
/// </summary>
public class SubscriptionContractTests
{
    [Fact]
    public void CreateSubscriptionRequest_SnakeCaseBody_IsDeserialized()
    {
        const string body = """
            {
              "user_id": "11111111-1111-1111-1111-111111111111",
              "plan_id": "22222222-2222-2222-2222-222222222222",
              "stripe_customer_id": "cus_test_123"
            }
            """;

        var request = JsonSerializer.Deserialize<SubscriptionResources.CreateSubscriptionRequest>(body);

        Assert.NotNull(request);
        Assert.Equal("11111111-1111-1111-1111-111111111111", request!.UserId);
        Assert.Equal("22222222-2222-2222-2222-222222222222", request.PlanId);
        Assert.Equal("cus_test_123", request.StripeCustomerId);
    }

    [Fact]
    public void SubscriptionResource_Serialized_UsesPascalCase()
    {
        var subscription = Subscription.Start(
            Guid.NewGuid().ToString(), Guid.NewGuid(), "sub_test_123");

        var json = JsonSerializer.Serialize(SubscriptionResources.SubscriptionResource.From(subscription));

        Assert.Contains("\"SubscriptionID\"", json);
        Assert.Contains("\"UserID\"", json);
        Assert.Contains("\"PlanID\"", json);
        Assert.Contains("\"Status\"", json);
        Assert.Contains("\"StripeSubscriptionID\"", json);
        // camelCase aqui rompe el frontend en silencio.
        Assert.DoesNotContain("\"subscriptionId\"", json);
        Assert.DoesNotContain("\"user_id\"", json);
    }

    [Fact]
    public void PlanResource_Serialized_IncludesItsFeaturesAlsoInPascalCase()
    {
        var plan = SubscriptionPlan.Create("Pro", "Plan intermedio", 79.9, "PEN", "monthly");
        plan.AddFeature("SITES_LIMIT", "Locales incluidos", "5");

        var json = JsonSerializer.Serialize(SubscriptionResources.PlanResource.From(plan));

        Assert.Contains("\"PlanID\"", json);
        Assert.Contains("\"BillingPeriod\"", json);
        Assert.Contains("\"PlanFeatures\"", json);
        Assert.Contains("\"FeatureCode\"", json);
        Assert.Contains("\"FeatureValue\"", json);
    }

    [Fact]
    public void StripePriceId_PlanFeatures_OnlyTheStripePriceFeatureCounts()
    {
        var plan = SubscriptionPlan.Create("Pro", null, 79.9, null, null);
        Assert.Null(plan.StripePriceId());

        plan.AddFeature("SITES_LIMIT", "Locales incluidos", "5");
        Assert.Null(plan.StripePriceId());

        plan.AddFeature(PlanFeature.StripePriceIdCode, "Precio en Stripe", "price_test_123");
        Assert.Equal("price_test_123", plan.StripePriceId());
    }

    [Fact]
    public void ToSubscriptionStatus_UnknownStatus_ThrowsValidationErrorInsteadOfAssumingOne()
    {
        Assert.Equal(SubscriptionStatus.ACTIVE,
            SubscriptionStatusExtensions.ToSubscriptionStatus("active"));

        var error = Assert.Throws<AppException>(
            () => SubscriptionStatusExtensions.ToSubscriptionStatus("SUSPENDED"));
        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
    }

    [Fact]
    public void Cancel_ActiveSubscription_LeavesItInAFinalStatus()
    {
        var subscription = Subscription.Start(Guid.NewGuid().ToString(), Guid.NewGuid(), null);
        Assert.False(subscription.Status.IsFinal());

        subscription.Cancel();

        Assert.Equal(SubscriptionStatus.CANCELLED, subscription.Status);
        Assert.True(subscription.Status.IsFinal());
        Assert.NotNull(subscription.EndDate);
    }
}
