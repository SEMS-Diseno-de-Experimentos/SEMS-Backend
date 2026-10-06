using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sems.Api.Modules.Subscriptions.Domain.Model;
using Sems.Api.Modules.Subscriptions.Domain.Repositories;
using Sems.Api.Modules.Subscriptions.Infrastructure;
using Xunit;

namespace Sems.Api.Tests.Subscriptions;

/// <summary>
/// Carga de los planes por defecto. Los planes se miden en locales
/// (<c>SITES_LIMIT</c>), no en dispositivos.
/// </summary>
public class PlanSeederTests
{
    private readonly IPlanRepository _plans = Substitute.For<IPlanRepository>();
    private readonly List<SubscriptionPlan> _saved = new();
    private readonly PlanSeeder _seeder;

    public PlanSeederTests()
    {
        _plans.SaveAsync(Arg.Do<SubscriptionPlan>(_saved.Add), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<SubscriptionPlan>());

        _seeder = new PlanSeeder(_plans, new ConfigurationBuilder().Build(),
            NullLogger<PlanSeeder>.Instance);
    }

    private static string? Feature(SubscriptionPlan plan, string code) =>
        plan.PlanFeatures.SingleOrDefault(f => f.FeatureCode == code)?.FeatureValue;

    [Fact]
    public async Task SeedAsync_EmptyCatalog_LoadsBasicoProAndEnterprise()
    {
        await _seeder.SeedAsync();

        Assert.Equal(new[] { "Básico", "Pro", "Enterprise" }, _saved.Select(p => p.Name));
        Assert.All(_saved, p => Assert.True(p.Active));
    }

    [Theory]
    [InlineData("Básico", "1", 29.90)]
    [InlineData("Pro", "5", 79.90)]
    [InlineData("Enterprise", "ilimitado", 129.90)]
    public async Task SeedAsync_EmptyCatalog_GivesEachPlanItsSitesLimitAndPrice(string name,
        string sitesLimit, double price)
    {
        await _seeder.SeedAsync();

        var plan = _saved.Single(p => p.Name == name);
        Assert.Equal(sitesLimit, Feature(plan, "SITES_LIMIT"));
        Assert.Equal(price, plan.Price);
        Assert.Equal("PEN", plan.Currency);
    }

    [Fact]
    public async Task SeedAsync_AllPlans_HaveSitesLimitAndNoneKeepsTheOldDevicesLimit()
    {
        await _seeder.SeedAsync();

        Assert.All(_saved, plan =>
        {
            Assert.NotNull(Feature(plan, "SITES_LIMIT"));
            Assert.Null(Feature(plan, "LINKED_DEVICES_LIMIT"));
        });
    }

    [Fact]
    public async Task SeedAsync_CatalogAlreadyLoaded_DoesNothing()
    {
        _plans.CountAsync(Arg.Any<CancellationToken>()).Returns(3);

        await _seeder.SeedAsync();

        await _plans.DidNotReceive().SaveAsync(Arg.Any<SubscriptionPlan>(), Arg.Any<CancellationToken>());
    }
}
