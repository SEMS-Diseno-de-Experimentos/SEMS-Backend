using System.Net;
using System.Text.Json;
using Sems.Api.TestSupport;

namespace Sems.Api.IntegrationTests.Devices;

/// <summary>
/// Alta, traslado y baja de medidores en <c>/api/v1/device-management</c>.
/// Contrato en camelCase.
/// </summary>
public class DeviceEndpointsTests : IClassFixture<SemsApiFactory>
{
    private readonly SemsApiFactory _factory;

    public DeviceEndpointsTests(SemsApiFactory factory) => _factory = factory;

    /// <summary>Un supervisor con dos locales, cada uno con su zona.</summary>
    private sealed record Layout(TestUser User, Guid OrganizationId, Guid SiteId, Guid ZoneId,
        Guid OtherSiteId, Guid OtherZoneId);

    private async Task<Layout> LayoutAsync()
    {
        var user = await _factory.CreateUserAsync();
        var organization = await user.Client.CreateOrganizationAsync(user.UserId);
        var organizationId = organization.Id("organization_id");
        var site = await user.Client.CreateSiteAsync(organizationId, "T-001");
        var zone = await user.Client.CreateZoneAsync(site.Id("site_id"), "Cold rooms", "COLD_STORAGE");
        var otherSite = await user.Client.CreateSiteAsync(organizationId, "T-002");
        var otherZone = await user.Client.CreateZoneAsync(otherSite.Id("site_id"), "Kitchen", "KITCHEN");
        return new Layout(user, organizationId, site.Id("site_id"), zone.Id("zone_id"),
            otherSite.Id("site_id"), otherZone.Id("zone_id"));
    }

    private static string Code() => $"SM-{Guid.NewGuid():N}";

    private static IEnumerable<string> Ids(JsonElement listing) =>
        listing.EnumerateArray().Select(d => d.Text("deviceId"));

    // -------------------------------------------------------------------- alta

    [Fact]
    public async Task Register_ActiveSiteAndOwnZone_Returns201Active()
    {
        var layout = await LayoutAsync();

        var response = await layout.User.Client.PostDeviceAsync(layout.User.UserId,
            layout.SiteId.ToString(), layout.ZoneId.ToString(), Code());

        var device = await response.ExpectAsync(HttpStatusCode.Created);
        Assert.Equal("ACTIVE", device.Text("status"));
        Assert.Equal(layout.SiteId, device.Id("siteId"));
        Assert.Equal(layout.ZoneId, device.Id("zoneId"));
    }

    [Fact]
    public async Task Register_ZoneOfAnotherSite_Returns400()
    {
        var layout = await LayoutAsync();

        var response = await layout.User.Client.PostDeviceAsync(layout.User.UserId,
            layout.SiteId.ToString(), layout.OtherZoneId.ToString(), Code());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_UnknownSite_Returns404()
    {
        var layout = await LayoutAsync();

        var response = await layout.User.Client.PostDeviceAsync(layout.User.UserId,
            Guid.NewGuid().ToString(), null, Code());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Register_ArchivedSite_Returns404()
    {
        var layout = await LayoutAsync();
        await layout.User.Client.DeleteAsync($"/api/v1/sites/{layout.OtherSiteId}");

        var response = await layout.User.Client.PostDeviceAsync(layout.User.UserId,
            layout.OtherSiteId.ToString(), null, Code());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Register_RepeatedExternalCode_Returns409()
    {
        var layout = await LayoutAsync();
        var code = Code();
        await layout.User.Client.CreateDeviceAsync(layout.User.UserId, layout.SiteId, layout.ZoneId, code);

        var response = await layout.User.Client.PostDeviceAsync(layout.User.UserId,
            layout.SiteId.ToString(), layout.ZoneId.ToString(), code);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_SiteIdThatIsNotUuid_Returns400()
    {
        var layout = await LayoutAsync();

        var response = await layout.User.Client.PostDeviceAsync(layout.User.UserId,
            "not-a-uuid", null, Code());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithoutToken_Returns401()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.PostDeviceAsync(Guid.NewGuid(), Guid.NewGuid().ToString(),
            null, Code());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------------------------------------------------------------- traslado

    private static object UpdateBody(Guid zoneId) => new
    {
        deviceName = "Compressor submeter",
        deviceType = "REFRIGERATION",
        brand = "Schneider",
        model = "PM5110",
        connectionProtocol = "WIFI",
        zoneId = zoneId.ToString()
    };

    [Fact]
    public async Task Update_ZoneOfTheSameSite_Returns200()
    {
        var layout = await LayoutAsync();
        var device = await layout.User.Client.CreateDeviceAsync(layout.User.UserId, layout.SiteId, null);
        var sameSiteZone = await layout.User.Client.CreateZoneAsync(layout.SiteId, "Sales floor", "SALES_FLOOR");

        var response = await layout.User.Client.PutJsonAsync(
            $"/api/v1/device-management/devices/{device.Text("deviceId")}",
            UpdateBody(sameSiteZone.Id("zone_id")));

        var updated = await response.ExpectAsync(HttpStatusCode.OK);
        Assert.Equal(sameSiteZone.Id("zone_id"), updated.Id("zoneId"));
        Assert.Equal(layout.SiteId, updated.Id("siteId"));
    }

    [Fact]
    public async Task Update_ZoneOfAnotherSite_Returns400()
    {
        var layout = await LayoutAsync();
        var device = await layout.User.Client.CreateDeviceAsync(layout.User.UserId, layout.SiteId, layout.ZoneId);

        var response = await layout.User.Client.PutJsonAsync(
            $"/api/v1/device-management/devices/{device.Text("deviceId")}",
            UpdateBody(layout.OtherZoneId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -------------------------------------------------------------------- baja

    [Fact]
    public async Task Remove_Device_Returns204AndLeavesTheSiteAndUserListings()
    {
        var layout = await LayoutAsync();
        var kept = await layout.User.Client.CreateDeviceAsync(layout.User.UserId, layout.SiteId, layout.ZoneId);
        var removed = await layout.User.Client.CreateDeviceAsync(layout.User.UserId, layout.SiteId, layout.ZoneId);

        var response = await layout.User.Client.DeleteAsync(
            $"/api/v1/device-management/devices/{removed.Text("deviceId")}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var bySite = await (await layout.User.Client.GetAsync(
            $"/api/v1/device-management/sites/{layout.SiteId}/devices")).ExpectAsync(HttpStatusCode.OK);
        var byZone = await (await layout.User.Client.GetAsync(
            $"/api/v1/device-management/zones/{layout.ZoneId}/devices")).ExpectAsync(HttpStatusCode.OK);
        var byUser = await (await layout.User.Client.GetAsync(
            $"/api/v1/device-management/users/{layout.User.UserId}/devices")).ExpectAsync(HttpStatusCode.OK);
        Assert.Equal(new[] { kept.Text("deviceId") }, Ids(bySite));
        Assert.Equal(new[] { kept.Text("deviceId") }, Ids(byZone));
        Assert.Equal(new[] { kept.Text("deviceId") }, Ids(byUser));
    }

    [Fact]
    public async Task RemoveDevice_DeviceNoLongerAppearsInTheGeneralListing()
    {
        var layout = await LayoutAsync();
        var device = await layout.User.Client.CreateDeviceAsync(layout.User.UserId, layout.SiteId, null);

        await (await layout.User.Client.DeleteAsync(
            $"/api/v1/device-management/devices/{device.Text("deviceId")}")).ExpectAsync(HttpStatusCode.NoContent);

        var all = await (await layout.User.Client.GetAsync("/api/v1/device-management/devices"))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.DoesNotContain(device.Text("deviceId"), Ids(all));
    }

    [Fact]
    public async Task Remove_RemovedDevice_IsStillQueryableAsRemovedAndASecondRemovalReturns409()
    {
        var layout = await LayoutAsync();
        var device = await layout.User.Client.CreateDeviceAsync(layout.User.UserId, layout.SiteId, null);
        var url = $"/api/v1/device-management/devices/{device.Text("deviceId")}";
        await (await layout.User.Client.DeleteAsync(url)).ExpectAsync(HttpStatusCode.NoContent);

        var byId = await (await layout.User.Client.GetAsync(url)).ExpectAsync(HttpStatusCode.OK);
        var secondRemoval = await layout.User.Client.DeleteAsync(url);

        Assert.Equal("REMOVED", byId.Text("status"));
        Assert.Equal(device.Text("externalDeviceCode"), byId.Text("externalDeviceCode"));
        Assert.Equal(HttpStatusCode.Conflict, secondRemoval.StatusCode);
    }
}
