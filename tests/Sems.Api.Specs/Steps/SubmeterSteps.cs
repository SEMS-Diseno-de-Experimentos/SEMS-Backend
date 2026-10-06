using System.Net;
using Sems.Api.TestSupport;
using TechTalk.SpecFlow;

namespace Sems.Api.Specs.Steps;

/// <summary>Alta y baja de medidores (EP04).</summary>
[Binding]
public sealed class SubmeterSteps
{
    private readonly ApiWorld _world;

    public SubmeterSteps(ApiWorld world) => _world = world;

    private TestUser Supervisor => _world.SignedInUser;

    private static string ZoneTypeOf(string zoneName) => zoneName switch
    {
        "Cold rooms" => "COLD_STORAGE",
        "Kitchen" => "KITCHEN",
        "Sales floor" => "SALES_FLOOR",
        _ => "OTHER"
    };

    private async Task AddSiteWithZoneAsync(string siteCode, string zoneName)
    {
        var site = await Supervisor.Client.CreateSiteAsync(_world.OrganizationId!.Value, siteCode);
        var zone = await Supervisor.Client.CreateZoneAsync(site.Id("site_id"), zoneName,
            ZoneTypeOf(zoneName));
        _world.Sites[siteCode] = site.Id("site_id");
        _world.Zones[zoneName] = zone.Id("zone_id");
    }

    [Given(@"a signed-in supervisor with the site ""(.*)"" and its zone ""(.*)""")]
    public async Task GivenASignedInSupervisorWithTheSiteAndItsZone(string siteCode, string zoneName)
    {
        _world.User = await _world.Factory.CreateUserAsync();
        var organization = await Supervisor.Client.CreateOrganizationAsync(Supervisor.UserId);
        _world.OrganizationId = organization.Id("organization_id");
        await AddSiteWithZoneAsync(siteCode, zoneName);
    }

    [Given(@"the organization also has the site ""(.*)"" and its zone ""(.*)""")]
    public Task GivenTheOrganizationAlsoHasTheSiteAndItsZone(string siteCode, string zoneName) =>
        AddSiteWithZoneAsync(siteCode, zoneName);

    [When(@"the supervisor registers the submeter ""(.*)"" in the site ""(.*)"" and the zone ""(.*)""")]
    public async Task WhenTheSupervisorRegistersTheSubmeter(string code, string siteCode, string zoneName)
    {
        var response = await _world.RecordAsync(Supervisor.Client.PostDeviceAsync(Supervisor.UserId,
            _world.Sites[siteCode].ToString(), _world.Zones[zoneName].ToString(), code));
        if (response.StatusCode == HttpStatusCode.Created)
        {
            _world.Submeters[code] = _world.LastBody.Id("deviceId");
        }
    }

    [Given(@"the submeter ""(.*)"" is registered in the site ""(.*)""")]
    public async Task GivenTheSubmeterIsRegisteredInTheSite(string code, string siteCode)
    {
        var device = await Supervisor.Client.CreateDeviceAsync(Supervisor.UserId,
            _world.Sites[siteCode], null, code);
        _world.Submeters[code] = device.Id("deviceId");
    }

    [Then(@"the submeter is registered as ""(.*)""")]
    public void ThenTheSubmeterIsRegisteredAs(string status)
    {
        Assert.Equal(HttpStatusCode.Created, _world.Response.StatusCode);
        Assert.Equal(status, _world.LastBody.Text("status"));
    }

    [Then(@"the submeter belongs to the site ""(.*)"" and the zone ""(.*)""")]
    public void ThenTheSubmeterBelongsToTheSiteAndTheZone(string siteCode, string zoneName)
    {
        Assert.Equal(_world.Sites[siteCode], _world.LastBody.Id("siteId"));
        Assert.Equal(_world.Zones[zoneName], _world.LastBody.Id("zoneId"));
    }

    [When(@"the supervisor removes the submeter ""(.*)""")]
    public Task WhenTheSupervisorRemovesTheSubmeter(string code) =>
        _world.RecordAsync(Supervisor.Client.DeleteAsync(
            $"/api/v1/device-management/devices/{_world.Submeters[code]}"));

    [Then(@"the site ""(.*)"" lists only the submeter ""(.*)""")]
    public async Task ThenTheSiteListsOnlyTheSubmeter(string siteCode, string code)
    {
        Assert.Equal(HttpStatusCode.NoContent, _world.Response.StatusCode);
        var devices = await (await Supervisor.Client.GetAsync(
                $"/api/v1/device-management/sites/{_world.Sites[siteCode]}/devices"))
            .ExpectAsync(HttpStatusCode.OK);

        Assert.Equal(new[] { code }, devices.EnumerateArray().Select(d => d.Text("externalDeviceCode")));
    }

    [Then(@"the submeter ""(.*)"" is kept with the status ""(.*)""")]
    public async Task ThenTheSubmeterIsKeptWithTheStatus(string code, string status)
    {
        var device = await (await Supervisor.Client.GetAsync(
                $"/api/v1/device-management/devices/{_world.Submeters[code]}"))
            .ExpectAsync(HttpStatusCode.OK);

        Assert.Equal(status, device.Text("status"));
        Assert.Equal(code, device.Text("externalDeviceCode"));
    }
}
