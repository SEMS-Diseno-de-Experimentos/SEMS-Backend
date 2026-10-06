using System.Net;
using Sems.Api.TestSupport;

namespace Sems.Api.IntegrationTests.Alerts;

/// <summary>
/// Reglas de demanda y evaluacion de la demanda medida de un local de 120 kW.
/// Contrato en snake_case.
/// </summary>
public class DemandAlertEndpointsTests : IClassFixture<SemsApiFactory>
{
    private const string AlertSubject = "Consumption alert at your site";

    private readonly SemsApiFactory _factory;

    public DemandAlertEndpointsTests(SemsApiFactory factory) => _factory = factory;

    private async Task<(TestUser Manager, Guid SiteId)> ManagerWithSiteAsync()
    {
        var manager = await _factory.CreateUserAsync();
        var organization = await manager.Client.CreateOrganizationAsync(manager.UserId);
        var site = await manager.Client.CreateSiteAsync(organization.Id("organization_id"), "T-001", 120m);
        return (manager, site.Id("site_id"));
    }

    private async Task<(TestUser Manager, Guid SiteId)> ManagerWithRuleAsync()
    {
        var (manager, siteId) = await ManagerWithSiteAsync();
        await (await manager.Client.PostDemandRuleAsync(siteId, manager.UserId, 120, 85))
            .ExpectAsync(HttpStatusCode.Created);
        return (manager, siteId);
    }

    // --------------------------------------------------------------- reglas

    [Fact]
    public async Task CreateDemandRule_EightyFivePercent_Returns201WithWarningThresholdOf102()
    {
        var (manager, siteId) = await ManagerWithSiteAsync();

        var rule = await (await manager.Client.PostDemandRuleAsync(siteId, manager.UserId, 120, 85))
            .ExpectAsync(HttpStatusCode.Created);

        Assert.Equal(102m, rule.Number("warning_threshold_kw"));
        Assert.True(rule.GetProperty("active").GetBoolean());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task CreateDemandRule_WarningPercentOutOfRange_Returns400(double warningPercent)
    {
        var (manager, siteId) = await ManagerWithSiteAsync();

        var response = await manager.Client.PostDemandRuleAsync(siteId, manager.UserId, 120, warningPercent);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------------ evaluacion

    [Fact]
    public async Task EvaluateDemand_BelowTheThreshold_Returns200WithAnEmptyList()
    {
        var (manager, siteId) = await ManagerWithRuleAsync();

        var alerts = await (await manager.Client.PostDemandEvaluationAsync(siteId, 101.9))
            .ExpectAsync(HttpStatusCode.OK);

        Assert.Empty(alerts.EnumerateArray());
    }

    [Theory]
    [InlineData(102, "18 kW of headroom left")]
    [InlineData(120, "0 kW of headroom left")]
    public async Task EvaluateDemand_BetweenThresholdAndContracted_RaisesWarningWithTheHeadroom(
        double demandKw, string headroom)
    {
        var (manager, siteId) = await ManagerWithRuleAsync();

        var alerts = await (await manager.Client.PostDemandEvaluationAsync(siteId, demandKw))
            .ExpectAsync(HttpStatusCode.OK);

        var alert = Assert.Single(alerts.EnumerateArray());
        Assert.Equal("WARNING", alert.Text("severity"));
        Assert.Contains(headroom, alert.Text("message"));
    }

    [Fact]
    public async Task EvaluateDemand_RaisedAlert_IsListedForTheUserAndNotifiedByEmail()
    {
        var (manager, siteId) = await ManagerWithRuleAsync();

        var alerts = await (await manager.Client.PostDemandEvaluationAsync(siteId, 130))
            .ExpectAsync(HttpStatusCode.OK);

        var alert = Assert.Single(alerts.EnumerateArray());
        Assert.Equal("CRITICAL", alert.Text("severity"));
        Assert.Contains("10 kW above the 120 kW contracted", alert.Text("message"));

        var listed = await (await manager.Client.GetAsync($"/api/v1/users/{manager.UserId}/alerts"))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.Contains(alert.Text("alert_id"), listed.EnumerateArray().Select(a => a.Text("alert_id")));

        var email = Assert.Single(_factory.Emails.SentTo(manager.Email, AlertSubject));
        Assert.Contains("10 kW above the 120 kW contracted", email.Body);
    }

    // ------------------------------------------------------------------ estado

    [Fact]
    public async Task UpdateAlertStatus_Resolved_Returns200WithTheResolutionDate()
    {
        var (manager, siteId) = await ManagerWithRuleAsync();
        var alerts = await (await manager.Client.PostDemandEvaluationAsync(siteId, 110))
            .ExpectAsync(HttpStatusCode.OK);
        var alertId = Assert.Single(alerts.EnumerateArray()).Text("alert_id");

        var response = await manager.Client.PatchJsonAsync($"/api/v1/alerts/{alertId}/status",
            new { status = "resolved" });

        var resolved = await response.ExpectAsync(HttpStatusCode.OK);
        Assert.Equal("resolved", resolved.Text("status"));
        Assert.Equal(System.Text.Json.JsonValueKind.String, resolved.GetProperty("resolved_at").ValueKind);
    }
}
