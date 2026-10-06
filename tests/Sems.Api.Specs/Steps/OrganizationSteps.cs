using System.Net;
using Sems.Api.TestSupport;
using TechTalk.SpecFlow;

namespace Sems.Api.Specs.Steps;

/// <summary>Organizacion, locales y zonas (EP03).</summary>
[Binding]
public sealed class OrganizationSteps
{
    private readonly ApiWorld _world;

    public OrganizationSteps(ApiWorld world) => _world = world;

    private HttpClient Client => _world.SignedInUser.Client;

    private Guid OrganizationId => _world.OrganizationId
        ?? throw new InvalidOperationException("El escenario no registro ninguna organizacion");

    [Given(@"an administrator is signed in")]
    public async Task GivenAnAdministratorIsSignedIn() =>
        _world.User = await _world.Factory.CreateUserAsync();

    [When(@"the administrator registers the organization ""(.*)"" with the RUC ""(.*)""")]
    public async Task WhenTheAdministratorRegistersTheOrganization(string legalName, string taxId)
    {
        var response = await _world.RecordAsync(
            Client.PostOrganizationAsync(_world.SignedInUser.UserId, taxId, legalName));
        if (response.StatusCode == HttpStatusCode.Created)
        {
            _world.OrganizationId = _world.LastBody.Id("organization_id");
        }
    }

    [Then(@"the organization is created")]
    public void ThenTheOrganizationIsCreated()
    {
        Assert.Equal(HttpStatusCode.Created, _world.Response.StatusCode);
        Assert.Equal("ACTIVE", _world.LastBody.Text("status"));
    }

    [Then(@"the administrator holds the ""(.*)"" role in it")]
    public async Task ThenTheAdministratorHoldsTheRoleInIt(string role)
    {
        var mine = await (await Client.GetAsync($"/api/v1/users/{_world.SignedInUser.UserId}/organizations"))
            .ExpectAsync(HttpStatusCode.OK);

        var membership = mine.EnumerateArray()
            .Single(m => m.GetProperty("organization").Id("organization_id") == OrganizationId);
        Assert.Equal(role, membership.Text("role"));
    }

    [Given(@"an organization with the RUC ""(.*)"" already exists")]
    public async Task GivenAnOrganizationWithTheRucAlreadyExists(string taxId)
    {
        var response = await Client.PostOrganizationAsync(_world.SignedInUser.UserId, taxId,
            "Original Owner S.A.C.");
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });
    }

    [Given(@"the administrator has registered an organization")]
    public async Task GivenTheAdministratorHasRegisteredAnOrganization()
    {
        var organization = await Client.CreateOrganizationAsync(_world.SignedInUser.UserId);
        _world.OrganizationId = organization.Id("organization_id");
    }

    [When(@"the administrator registers the site ""(.*)"" with (-?[\d.]+) kW contracted under the tariff ""(.*)""")]
    public async Task WhenTheAdministratorRegistersTheSite(string code, decimal power, string tariff)
    {
        var response = await _world.RecordAsync(Client.PostSiteAsync(OrganizationId, code, power, tariff));
        if (response.StatusCode == HttpStatusCode.Created)
        {
            _world.Sites[code] = _world.LastBody.Id("site_id");
        }
    }

    [Given(@"the organization has the site ""(.*)""")]
    public async Task GivenTheOrganizationHasTheSite(string code)
    {
        var site = await Client.CreateSiteAsync(OrganizationId, code);
        _world.Sites[code] = site.Id("site_id");
    }

    [Then(@"the site is registered in the organization")]
    public void ThenTheSiteIsRegisteredInTheOrganization()
    {
        Assert.Equal(HttpStatusCode.Created, _world.Response.StatusCode);
        Assert.Equal(OrganizationId, _world.LastBody.Id("organization_id"));
        Assert.Equal("ACTIVE", _world.LastBody.Text("status"));
    }

    [Then(@"the site charges for demand: (yes|no)")]
    public void ThenTheSiteChargesForDemand(string answer) =>
        Assert.Equal(answer == "yes", _world.LastBody.GetProperty("charges_for_demand").GetBoolean());

    [When(@"the administrator archives the site ""(.*)""")]
    public Task WhenTheAdministratorArchivesTheSite(string code) =>
        _world.RecordAsync(Client.DeleteAsync($"/api/v1/sites/{_world.Sites[code]}"));

    [Then(@"the site list contains only ""(.*)""")]
    public async Task ThenTheSiteListContainsOnly(string code)
    {
        Assert.Equal(HttpStatusCode.NoContent, _world.Response.StatusCode);
        var sites = await (await Client.GetAsync($"/api/v1/organizations/{OrganizationId}/sites"))
            .ExpectAsync(HttpStatusCode.OK);

        Assert.Equal(new[] { code }, sites.EnumerateArray().Select(s => s.Text("site_code")));
    }

    [When(@"the administrator registers the zone ""(.*)"" of type ""(.*)"" in the site ""(.*)""")]
    public Task WhenTheAdministratorRegistersTheZone(string name, string type, string siteCode) =>
        _world.RecordAsync(Client.PostZoneAsync(_world.Sites[siteCode], name, type));

    [Then(@"the zone is registered in that site")]
    public void ThenTheZoneIsRegisteredInThatSite()
    {
        Assert.Equal(HttpStatusCode.Created, _world.Response.StatusCode);
        Assert.Contains(_world.LastBody.Id("site_id"), _world.Sites.Values);
    }

    [Then(@"the zone operates off hours: (yes|no)")]
    public void ThenTheZoneOperatesOffHours(string answer) =>
        Assert.Equal(answer == "yes", _world.LastBody.GetProperty("operates_off_hours").GetBoolean());
}
