using Sems.Api.Shared.Errors;

namespace Sems.Api.Modules.Subscriptions.Domain.Model;

/// <summary>Estados posibles de una suscripcion.</summary>
public enum SubscriptionStatus
{
    ACTIVE,
    INACTIVE,
    CANCELLED,
    PENDING_RENEWAL,
    EXPIRED
}

public static class SubscriptionStatusExtensions
{
    public static SubscriptionStatus ToSubscriptionStatus(string? value) =>
        Enum.TryParse<SubscriptionStatus>(value?.Trim(), ignoreCase: true, out var s)
        && Enum.IsDefined(s)
            ? s
            : throw AppException.Validation("invalid subscription status");

    /// <summary>Una suscripcion cancelada o vencida ya no admite cambios.</summary>
    public static bool IsFinal(this SubscriptionStatus status) =>
        status is SubscriptionStatus.CANCELLED or SubscriptionStatus.EXPIRED;
}

/// <summary>
/// Caracteristica incluida en un plan.
///
/// <para>El valor es texto libre para poder expresar tanto interruptores
/// (<c>"enabled"</c>) como limites numericos (<c>"3"</c>), que es como el
/// frontend decide cuantos dispositivos permite cada plan.</para>
/// </summary>
public class PlanFeature
{
    public const string StripePriceIdCode = "STRIPE_PRICE_ID";

    public Guid FeatureId { get; private set; }
    public Guid PlanId { get; private set; }
    public string? FeatureCode { get; private set; }
    public string? FeatureName { get; private set; }
    public string? FeatureValue { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PlanFeature()
    {
    }

    public static PlanFeature Create(Guid planId, string? code, string? name, string? value) => new()
    {
        FeatureId = Guid.NewGuid(),
        PlanId = planId,
        FeatureCode = code,
        FeatureName = name,
        FeatureValue = value,
        CreatedAt = DateTime.UtcNow
    };
}

/// <summary>Plan comercial al que un usuario puede suscribirse.</summary>
public class SubscriptionPlan
{
    public Guid PlanId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public double Price { get; private set; }
    public string Currency { get; private set; } = "PEN";
    public string BillingPeriod { get; private set; } = "monthly";
    public bool Active { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>Relacion de navegacion: EF Core carga las caracteristicas con el plan.</summary>
    public List<PlanFeature> PlanFeatures { get; private set; } = new();

    private SubscriptionPlan()
    {
    }

    public static SubscriptionPlan Create(string name, string? description, double price,
        string? currency, string? billingPeriod) => new()
    {
        PlanId = Guid.NewGuid(),
        Name = name,
        Description = description,
        Price = price,
        Currency = string.IsNullOrWhiteSpace(currency) ? "PEN" : currency,
        BillingPeriod = string.IsNullOrWhiteSpace(billingPeriod) ? "monthly" : billingPeriod,
        Active = true,
        CreatedAt = DateTime.UtcNow,
        PlanFeatures = new List<PlanFeature>()
    };

    public void AddFeature(string? code, string? name, string? value) =>
        PlanFeatures.Add(PlanFeature.Create(PlanId, code, name, value));

    /// <summary>Identificador del precio en Stripe, si el plan esta enlazado.</summary>
    public string? StripePriceId() => PlanFeatures
        .FirstOrDefault(f => string.Equals(f.FeatureCode, PlanFeature.StripePriceIdCode,
            StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(f.FeatureValue))
        ?.FeatureValue;
}

/// <summary>
/// Suscripcion de un usuario a un plan.
///
/// <para><c>EndDate</c> y <c>StripeSubscriptionId</c> son opcionales: el primero
/// solo se llena al cancelar o vencer, y el segundo queda nulo cuando la
/// suscripcion no esta enlazada a Stripe.</para>
/// </summary>
public class Subscription
{
    public Guid SubscriptionId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public Guid PlanId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public string? StripeSubscriptionId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Subscription()
    {
    }

    public static Subscription Start(string userId, Guid planId, string? stripeSubscriptionId)
    {
        var now = DateTime.UtcNow;
        return new Subscription
        {
            SubscriptionId = Guid.NewGuid(),
            UserId = userId,
            PlanId = planId,
            Status = SubscriptionStatus.ACTIVE,
            StartDate = now,
            StripeSubscriptionId = stripeSubscriptionId,
            CreatedAt = now
        };
    }

    public void Cancel()
    {
        Status = SubscriptionStatus.CANCELLED;
        EndDate = DateTime.UtcNow;
    }

    public void ChangePlan(Guid newPlanId) => PlanId = newPlanId;

    public void UpdateStatus(SubscriptionStatus next) => Status = next;

    public void LinkToStripe(string stripeSubscriptionId) =>
        StripeSubscriptionId = stripeSubscriptionId;
}
