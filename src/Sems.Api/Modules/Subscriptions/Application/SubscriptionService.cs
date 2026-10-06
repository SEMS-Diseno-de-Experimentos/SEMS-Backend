using Sems.Api.Modules.Subscriptions.Domain.Model;
using Sems.Api.Modules.Subscriptions.Domain.Repositories;
using Sems.Api.Modules.Subscriptions.Domain.Services;
using Sems.Api.Shared.Errors;
using Sems.Api.Shared.Events;

namespace Sems.Api.Modules.Subscriptions.Application;

/// <summary>Casos de uso del modulo de suscripciones.</summary>
public sealed class SubscriptionService
{
    private readonly IPlanRepository _plans;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly SubscriptionManager _manager;
    private readonly IDomainEventBus _bus;

    public SubscriptionService(IPlanRepository plans, ISubscriptionRepository subscriptions,
        SubscriptionManager manager, IDomainEventBus bus)
    {
        _plans = plans;
        _subscriptions = subscriptions;
        _manager = manager;
        _bus = bus;
    }

    // ------------------------------------------------------------------ planes

    public Task<List<SubscriptionPlan>> ActivePlansAsync(CancellationToken ct = default) =>
        _plans.FindAllActiveAsync(ct);

    public async Task<SubscriptionPlan> PlanByIdAsync(Guid planId, CancellationToken ct = default) =>
        await _plans.FindByIdAsync(planId, ct) ?? throw AppException.NotFound("plan not found");

    // ----------------------------------------------------------- suscripciones

    public async Task<Subscription> SubscriptionByIdAsync(Guid subscriptionId,
        CancellationToken ct = default) =>
        await _subscriptions.FindByIdAsync(subscriptionId, ct)
        ?? throw AppException.NotFound("subscription not found");

    public Task<List<Subscription>> SubscriptionsByUserAsync(string userId,
        CancellationToken ct = default) => _subscriptions.FindByUserIdAsync(userId, ct);

    public async Task<Subscription> CreateAsync(string userId, Guid planId,
        string? stripeSubscriptionId, CancellationToken ct = default)
    {
        // El plan debe existir antes de cobrar nada.
        await PlanByIdAsync(planId, ct);

        var subscription = Subscription.Start(userId, planId, stripeSubscriptionId);
        await PublishChangeAsync(subscription, ct);
        return await _subscriptions.SaveAsync(subscription, ct);
    }

    public async Task<Subscription> CancelAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        var subscription = await SubscriptionByIdAsync(subscriptionId, ct);
        _manager.EnsureCanCancel(subscription.Status);
        subscription.Cancel();
        await PublishChangeAsync(subscription, ct);
        return await _subscriptions.SaveAsync(subscription, ct);
    }

    public async Task<Subscription> ChangePlanAsync(Guid subscriptionId, Guid newPlanId,
        CancellationToken ct = default)
    {
        var subscription = await SubscriptionByIdAsync(subscriptionId, ct);
        _manager.EnsureCanChangePlan(subscription.Status);
        await PlanByIdAsync(newPlanId, ct);
        subscription.ChangePlan(newPlanId);
        await PublishChangeAsync(subscription, ct);
        return await _subscriptions.SaveAsync(subscription, ct);
    }

    /// <summary>Usado por el webhook de Stripe para reflejar el estado real del cobro.</summary>
    public async Task<Subscription> UpdateStatusFromStripeAsync(string stripeSubscriptionId,
        SubscriptionStatus status, CancellationToken ct = default)
    {
        var subscription = await _subscriptions.FindByStripeSubscriptionIdAsync(stripeSubscriptionId, ct)
                           ?? throw AppException.NotFound("subscription not found for stripe id");

        subscription.UpdateStatus(status);
        await PublishChangeAsync(subscription, ct);
        return await _subscriptions.SaveAsync(subscription, ct);
    }

    /// <summary>
    /// Avisa al resto del sistema del cambio de plan.
    ///
    /// <para>Antes viajaba por el topic <c>subscriptions.events</c>.</para>
    /// </summary>
    private async Task PublishChangeAsync(Subscription subscription, CancellationToken ct)
    {
        if (!Guid.TryParse(subscription.UserId, out var userGuid))
        {
            // El identificador no es un UUID: no se emite el evento, pero la
            // operacion de negocio no debe fallar por eso.
            return;
        }

        var plan = await _plans.FindByIdAsync(subscription.PlanId, ct);
        _bus.Publish(new DomainEvents.SubscriptionChanged(userGuid, subscription.SubscriptionId,
            plan?.Name ?? "unknown", subscription.Status.ToString()));
    }
}
