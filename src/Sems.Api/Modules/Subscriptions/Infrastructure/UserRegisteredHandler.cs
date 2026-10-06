using Sems.Api.Modules.Subscriptions.Application;
using Sems.Api.Modules.Subscriptions.Domain.Repositories;
using Sems.Api.Shared.Events;

namespace Sems.Api.Modules.Subscriptions.Infrastructure;

public sealed class SubscriptionUserRegisteredHandler : IDomainEventHandler<DomainEvents.UserRegistered>
{
    private readonly SubscriptionService _subscriptions;
    private readonly IPlanRepository _plans;
    private readonly ILogger<SubscriptionUserRegisteredHandler> _logger;

    public SubscriptionUserRegisteredHandler(SubscriptionService subscriptions, IPlanRepository plans, ILogger<SubscriptionUserRegisteredHandler> logger)
    {
        _subscriptions = subscriptions;
        _plans = plans;
        _logger = logger;
    }

    public async Task HandleAsync(DomainEvents.UserRegistered e, CancellationToken ct = default)
    {
        var plans = await _plans.FindAllActiveAsync(ct);
        var freePlan = plans.FirstOrDefault(p => p.Price == 0 || p.Name == "Básico");

        if (freePlan != null)
        {
            await _subscriptions.CreateAsync(e.UserId.ToString(), freePlan.PlanId, null, ct);
            _logger.LogInformation("Assigned free plan {PlanId} to user {UserId}", freePlan.PlanId, e.UserId);
        }
        else
        {
            _logger.LogWarning("No free plan found to assign to user {UserId}", e.UserId);
        }
    }
}
