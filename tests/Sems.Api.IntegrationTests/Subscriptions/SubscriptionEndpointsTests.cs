using System.Net;
using System.Text.Json;
using Sems.Api.TestSupport;

namespace Sems.Api.IntegrationTests.Subscriptions;

/// <summary>
/// Planes y suscripciones. El contrato es asimetrico a proposito: peticion en
/// snake_case y respuesta en PascalCase.
/// </summary>
public class SubscriptionEndpointsTests : IClassFixture<SemsApiFactory>
{
    private readonly SemsApiFactory _factory;

    public SubscriptionEndpointsTests(SemsApiFactory factory) => _factory = factory;

    private static async Task<JsonElement> PlanAsync(HttpClient client, string name)
    {
        var plans = await (await client.GetAsync("/api/v1/subscription-plans")).ExpectAsync(HttpStatusCode.OK);
        return plans.EnumerateArray().Single(p => p.Text("Name") == name);
    }

    private static string SitesLimit(JsonElement plan) => plan.GetProperty("PlanFeatures")
        .EnumerateArray().Single(f => f.Text("FeatureCode") == "SITES_LIMIT").Text("FeatureValue");

    [Fact]
    public async Task GetPlans_Authenticated_Returns200WithThreePlansAndSitesLimitInPascalCase()
    {
        var user = await _factory.CreateUserAsync();

        var plans = await (await user.Client.GetAsync("/api/v1/subscription-plans"))
            .ExpectAsync(HttpStatusCode.OK);

        Assert.Equal(new[] { "Básico", "Pro", "Enterprise" },
            plans.EnumerateArray().Select(p => p.Text("Name")));
        Assert.Equal(new[] { "1", "5", "ilimitado" }, plans.EnumerateArray().Select(SitesLimit));
        Assert.All(plans.EnumerateArray(), p =>
        {
            Assert.True(p.TryGetProperty("PlanID", out _));
            Assert.False(p.TryGetProperty("plan_id", out _));
        });
    }

    [Fact]
    public async Task Create_SnakeCaseRequest_Returns201WithPascalCaseResponse()
    {
        var user = await _factory.CreateUserAsync();
        var pro = await PlanAsync(user.Client, "Pro");

        var response = await user.Client.PostJsonAsync("/api/v1/subscriptions",
            new { user_id = user.UserId.ToString(), plan_id = pro.Text("PlanID") });

        var subscription = await response.ExpectAsync(HttpStatusCode.Created);
        Assert.Equal("ACTIVE", subscription.Text("Status"));
        Assert.Equal(pro.Text("PlanID"), subscription.Text("PlanID"));
        Assert.Equal(user.UserId.ToString(), subscription.Text("UserID"));
        Assert.False(subscription.TryGetProperty("subscription_id", out _));
    }

    [Fact]
    public async Task Create_UnknownPlan_Returns404()
    {
        var user = await _factory.CreateUserAsync();

        var response = await user.Client.PostJsonAsync("/api/v1/subscriptions",
            new { user_id = user.UserId.ToString(), plan_id = Guid.NewGuid().ToString() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_Twice_SecondReturns409()
    {
        var user = await _factory.CreateUserAsync();
        var pro = await PlanAsync(user.Client, "Pro");
        var subscription = await (await user.Client.PostJsonAsync("/api/v1/subscriptions",
                new { user_id = user.UserId.ToString(), plan_id = pro.Text("PlanID") }))
            .ExpectAsync(HttpStatusCode.Created);
        var url = $"/api/v1/subscriptions/{subscription.Text("SubscriptionID")}/cancel";

        var first = await user.Client.PatchAsync(url, null);
        var second = await user.Client.PatchAsync(url, null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("CANCELLED", (await first.ReadJsonAsync()).Text("Status"));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_NewAccount_IsSubscribedToTheEntryPlan()
    {
        var user = await _factory.CreateUserAsync();
        var basico = await PlanAsync(user.Client, "Básico");

        var subscriptions = await (await user.Client.GetAsync($"/api/v1/subscriptions/users/{user.UserId}"))
            .ExpectAsync(HttpStatusCode.OK);

        var subscription = Assert.Single(subscriptions.EnumerateArray());
        Assert.Equal(basico.Text("PlanID"), subscription.Text("PlanID"));
        Assert.Equal("ACTIVE", subscription.Text("Status"));
    }
}
