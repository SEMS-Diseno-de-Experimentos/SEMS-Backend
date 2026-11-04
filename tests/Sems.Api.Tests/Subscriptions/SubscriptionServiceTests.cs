using NSubstitute;
using Sems.Api.Modules.Subscriptions.Application;
using Sems.Api.Modules.Subscriptions.Domain.Model;
using Sems.Api.Modules.Subscriptions.Domain.Repositories;
using Sems.Api.Modules.Subscriptions.Domain.Services;
using Sems.Api.Shared.Errors;
using Sems.Api.Shared.Events;
using Xunit;

namespace Sems.Api.Tests.Subscriptions;

/// <summary>
/// Alta, cancelacion y cambio de plan, con los repositorios y el bus simulados.
/// </summary>
public class SubscriptionServiceTests
{
    private readonly IPlanRepository _plans = Substitute.For<IPlanRepository>();
    private readonly ISubscriptionRepository _subscriptions = Substitute.For<ISubscriptionRepository>();
    private readonly IDomainEventBus _bus = Substitute.For<IDomainEventBus>();
    private readonly SubscriptionService _service;
    private readonly SubscriptionPlan _pro = SubscriptionPlan.Create("Pro", null, 79.90, "PEN", "monthly");

    public SubscriptionServiceTests()
    {
        _plans.FindByIdAsync(_pro.PlanId, Arg.Any<CancellationToken>()).Returns(_pro);
        _subscriptions.SaveAsync(Arg.Any<Subscription>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Subscription>());

        _service = new SubscriptionService(_plans, _subscriptions, new SubscriptionManager(), _bus);
    }

    private Subscription Stored(Action<Subscription>? arrange = null)
    {
        var subscription = Subscription.Start(Guid.NewGuid().ToString(), _pro.PlanId, null);
        arrange?.Invoke(subscription);
        _subscriptions.FindByIdAsync(subscription.SubscriptionId, Arg.Any<CancellationToken>())
            .Returns(subscription);
        return subscription;
    }

    [Fact]
    public async Task CreateAsync_ExistingPlan_StartsActiveAndPublishesSubscriptionChanged()
    {
        var userId = Guid.NewGuid();

        var subscription = await _service.CreateAsync(userId.ToString(), _pro.PlanId, null);

        Assert.Equal(SubscriptionStatus.ACTIVE, subscription.Status);
        await _subscriptions.Received(1).SaveAsync(subscription, Arg.Any<CancellationToken>());
        _bus.Received(1).Publish(Arg.Is<DomainEvents.SubscriptionChanged>(e =>
            e.UserId == userId && e.PlanName == "Pro" && e.Status == "ACTIVE"));
    }

    [Fact]
    public async Task CreateAsync_UnknownPlan_ThrowsNotFound()
    {
        var error = await Assert.ThrowsAsync<AppException>(
            () => _service.CreateAsync(Guid.NewGuid().ToString(), Guid.NewGuid(), null));

        Assert.Equal(ErrorCode.NOT_FOUND, error.Code);
        await _subscriptions.DidNotReceive().SaveAsync(Arg.Any<Subscription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAsync_ActiveSubscription_EndsItCancelled()
    {
        var subscription = Stored();

        var cancelled = await _service.CancelAsync(subscription.SubscriptionId);

        Assert.Equal(SubscriptionStatus.CANCELLED, cancelled.Status);
        Assert.NotNull(cancelled.EndDate);
    }

    [Fact]
    public async Task CancelAsync_AlreadyCancelledSubscription_ThrowsConflict()
    {
        var subscription = Stored(s => s.Cancel());

        var error = await Assert.ThrowsAsync<AppException>(
            () => _service.CancelAsync(subscription.SubscriptionId));

        Assert.Equal(ErrorCode.CONFLICT, error.Code);
    }

    [Fact]
    public async Task ChangePlanAsync_ExpiredSubscription_ThrowsConflict()
    {
        var subscription = Stored(s => s.UpdateStatus(SubscriptionStatus.EXPIRED));

        var error = await Assert.ThrowsAsync<AppException>(
            () => _service.ChangePlanAsync(subscription.SubscriptionId, _pro.PlanId));

        Assert.Equal(ErrorCode.CONFLICT, error.Code);
    }

    [Fact]
    public async Task ChangePlanAsync_UnknownNewPlan_ThrowsNotFound()
    {
        var subscription = Stored();

        var error = await Assert.ThrowsAsync<AppException>(
            () => _service.ChangePlanAsync(subscription.SubscriptionId, Guid.NewGuid()));

        Assert.Equal(ErrorCode.NOT_FOUND, error.Code);
    }
}
