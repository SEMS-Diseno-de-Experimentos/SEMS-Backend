using Sems.Api.Modules.Subscriptions.Domain.Model;

namespace Sems.Api.Modules.Subscriptions.Domain.Repositories;

/// <summary>Puertos de salida del modulo de suscripciones.</summary>
public interface IPlanRepository
{
    Task<SubscriptionPlan> SaveAsync(SubscriptionPlan plan, CancellationToken ct = default);
    Task<SubscriptionPlan?> FindByIdAsync(Guid planId, CancellationToken ct = default);
    Task<SubscriptionPlan?> FindByNameAsync(string name, CancellationToken ct = default);
    Task<List<SubscriptionPlan>> FindAllActiveAsync(CancellationToken ct = default);
    Task<long> CountAsync(CancellationToken ct = default);
}

public interface ISubscriptionRepository
{
    Task<Subscription> SaveAsync(Subscription subscription, CancellationToken ct = default);
    Task<Subscription?> FindByIdAsync(Guid subscriptionId, CancellationToken ct = default);
    Task<List<Subscription>> FindByUserIdAsync(string userId, CancellationToken ct = default);
    Task<Subscription?> FindByStripeSubscriptionIdAsync(string stripeSubscriptionId, CancellationToken ct = default);
}
