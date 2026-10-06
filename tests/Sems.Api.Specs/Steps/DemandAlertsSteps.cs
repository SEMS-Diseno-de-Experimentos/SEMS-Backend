using System.Net;
using Sems.Api.TestSupport;
using TechTalk.SpecFlow;

namespace Sems.Api.Specs.Steps;

/// <summary>Alertas de demanda antes de superar la potencia contratada (EP06).</summary>
[Binding]
public sealed class DemandAlertsSteps
{
    private const string AlertSubject = "Consumption alert at your site";

    private readonly ApiWorld _world;
    private double _contractedPowerKw;
    private Guid _siteId;

    public DemandAlertsSteps(ApiWorld world) => _world = world;

    private HttpClient Client => _world.SignedInUser.Client;

    [Given(@"a signed-in manager with a site of ([\d.]+) kW contracted")]
    public async Task GivenASignedInManagerWithASite(decimal contractedPowerKw)
    {
        _world.User = await _world.Factory.CreateUserAsync();
        var organization = await Client.CreateOrganizationAsync(_world.User.UserId);
        var site = await Client.CreateSiteAsync(organization.Id("organization_id"), "T-001",
            contractedPowerKw);
        _siteId = site.Id("site_id");
        _contractedPowerKw = (double)contractedPowerKw;
    }

    [When(@"the manager creates a demand rule warning at ([\d.]+) percent")]
    public Task WhenTheManagerCreatesADemandRule(double percent) =>
        _world.RecordAsync(Client.PostDemandRuleAsync(_siteId, _world.SignedInUser.UserId,
            _contractedPowerKw, percent));

    [Given(@"the site has a demand rule warning at ([\d.]+) percent")]
    public async Task GivenTheSiteHasADemandRule(double percent) =>
        await (await Client.PostDemandRuleAsync(_siteId, _world.SignedInUser.UserId,
            _contractedPowerKw, percent)).ExpectAsync(HttpStatusCode.Created);

    [Then(@"the rule is active with a warning threshold of ([\d.]+) kW")]
    public void ThenTheRuleIsActiveWithAWarningThreshold(decimal thresholdKw)
    {
        Assert.Equal(HttpStatusCode.Created, _world.Response.StatusCode);
        Assert.True(_world.LastBody.GetProperty("active").GetBoolean());
        Assert.Equal(thresholdKw, _world.LastBody.Number("warning_threshold_kw"));
    }

    [When(@"a demand of ([\d.]+) kW is evaluated for the site")]
    public Task WhenADemandIsEvaluatedForTheSite(double demandKw) =>
        _world.RecordAsync(Client.PostDemandEvaluationAsync(_siteId, demandKw));

    [Then(@"the resulting alert is ""(.*)""")]
    public void ThenTheResultingAlertIs(string severity)
    {
        Assert.Equal(HttpStatusCode.OK, _world.Response.StatusCode);
        var alerts = _world.LastBody.EnumerateArray().ToList();

        if (severity == "none")
        {
            Assert.Empty(alerts);
            return;
        }

        Assert.Equal(severity, Assert.Single(alerts).Text("severity"));
    }

    [Then(@"the alert reports ""(.*)""")]
    public void ThenTheAlertReports(string text) =>
        Assert.Contains(text, _world.LastBody.EnumerateArray().Single().Text("message"));

    [Then(@"the manager receives the alert by email")]
    public void ThenTheManagerReceivesTheAlertByEmail()
    {
        var alert = _world.LastBody.EnumerateArray().Single();
        var email = Assert.Single(_world.Factory.Emails.SentTo(_world.SignedInUser.Email, AlertSubject));
        Assert.Contains(alert.Text("message"), email.Body);
        Assert.Contains(alert.Text("severity"), email.Body);
    }
}
