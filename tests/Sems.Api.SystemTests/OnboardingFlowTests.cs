using System.Net;
using System.Text.Json;
using Sems.Api.TestSupport;

namespace Sems.Api.SystemTests;

/// <summary>
/// Flujo 1: alta de un establecimiento (US06, US07, US12, US13, US14, US18,
/// US19, US20, US40).
/// </summary>
/// <remarks>
/// Registro, inicio de sesion, plan de entrada, organizacion, local, zona y
/// medidor, encadenando IAM, Subscriptions, Organizations y Device Management
/// en una misma prueba y comprobando el resultado en cada paso.
/// </remarks>
public class OnboardingFlowTests : IClassFixture<SemsApiFactory>
{
    private readonly SemsApiFactory _factory;
    private readonly HttpClient _anonymous;

    public OnboardingFlowTests(SemsApiFactory factory)
    {
        _factory = factory;
        _anonymous = factory.CreateClient();
    }

    private static IEnumerable<string> DeviceIds(JsonElement listing) =>
        listing.EnumerateArray().Select(d => d.Text("deviceId"));

    [Fact]
    public async Task Onboarding_NewEstablishment_EndsWithItsSubmeterListedBySiteZoneAndUser()
    {
        var email = $"maria.quispe-{Guid.NewGuid():N}@minimarket.pe";

        // US06: la persona crea su cuenta.
        var registered = await (await _anonymous.PostJsonAsync("/api/v1/auth/register",
            new { emailAddress = email, password = "SecurePass123" })).ExpectAsync(HttpStatusCode.OK);
        var userId = registered.GetProperty("userId").GetGuid();

        // US07: inicia sesion y recibe su token.
        var session = await (await _anonymous.PostJsonAsync("/api/v1/auth/login",
            new { emailAddress = email, password = "SecurePass123" })).ExpectAsync(HttpStatusCode.OK);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.Text("token"));

        var me = await (await client.GetAsync("/api/v1/users/me")).ExpectAsync(HttpStatusCode.OK);
        Assert.Equal(email, me.Text("emailAddress"));
        Assert.Equal(userId, me.GetProperty("userId").GetGuid());

        // US40: consulta los planes y comprueba que tiene asignado el de entrada.
        var plans = await (await client.GetAsync("/api/v1/subscription-plans")).ExpectAsync(HttpStatusCode.OK);
        var entryPlan = plans.EnumerateArray().Single(p => p.Text("Name") == "Básico");
        var subscriptions = await (await client.GetAsync($"/api/v1/subscriptions/users/{userId}"))
            .ExpectAsync(HttpStatusCode.OK);
        var subscription = Assert.Single(subscriptions.EnumerateArray());
        Assert.Equal(entryPlan.Text("PlanID"), subscription.Text("PlanID"));
        Assert.Equal("ACTIVE", subscription.Text("Status"));

        // US12: registra la organizacion y queda como administrador.
        var organization = await client.CreateOrganizationAsync(userId, ApiRequests.NewTaxId(),
            "Minimarket Los Andes S.A.C.");
        var organizationId = organization.Id("organization_id");
        var mine = await (await client.GetAsync($"/api/v1/users/{userId}/organizations"))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.Equal("ORG_ADMIN", Assert.Single(mine.EnumerateArray()).Text("role"));

        // US13 y US14: registra el local T-001 con 120 kW en MT2 y lo ve en la lista.
        var site = await client.CreateSiteAsync(organizationId, "T-001", 120m, "MT2");
        var siteId = site.Id("site_id");
        Assert.True(site.GetProperty("charges_for_demand").GetBoolean());
        var sites = await (await client.GetAsync($"/api/v1/organizations/{organizationId}/sites"))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.Equal(new[] { "T-001" }, sites.EnumerateArray().Select(s => s.Text("site_code")));

        // US18: divide el local en zonas.
        var zone = await client.CreateZoneAsync(siteId, "Cold rooms", "COLD_STORAGE");
        var zoneId = zone.Id("zone_id");
        Assert.True(zone.GetProperty("operates_off_hours").GetBoolean());

        // US19: registra el medidor en esa zona.
        var device = await client.CreateDeviceAsync(userId, siteId, zoneId, "SM-0001");
        Assert.Equal("ACTIVE", device.Text("status"));

        // US20: el medidor aparece en los listados por local, por zona y por usuario.
        var bySite = await (await client.GetAsync($"/api/v1/device-management/sites/{siteId}/devices"))
            .ExpectAsync(HttpStatusCode.OK);
        var byZone = await (await client.GetAsync($"/api/v1/device-management/zones/{zoneId}/devices"))
            .ExpectAsync(HttpStatusCode.OK);
        var byUser = await (await client.GetAsync($"/api/v1/device-management/users/{userId}/devices"))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.Equal(new[] { device.Text("deviceId") }, DeviceIds(bySite));
        Assert.Equal(new[] { device.Text("deviceId") }, DeviceIds(byZone));
        Assert.Equal(new[] { device.Text("deviceId") }, DeviceIds(byUser));
    }

    [Fact]
    public async Task Onboarding_SubmeterInAZoneOfAnotherSite_IsRejectedAndNotListed()
    {
        var user = await _factory.CreateUserAsync();
        var organization = await user.Client.CreateOrganizationAsync(user.UserId);
        var organizationId = organization.Id("organization_id");
        var site = await user.Client.CreateSiteAsync(organizationId, "T-001");
        var otherSite = await user.Client.CreateSiteAsync(organizationId, "T-002");
        var foreignZone = await user.Client.CreateZoneAsync(otherSite.Id("site_id"), "Kitchen", "KITCHEN");

        var response = await user.Client.PostDeviceAsync(user.UserId, site.Text("site_id"),
            foreignZone.Text("zone_id"), "SM-0002");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var bySite = await (await user.Client.GetAsync(
            $"/api/v1/device-management/sites/{site.Text("site_id")}/devices")).ExpectAsync(HttpStatusCode.OK);
        var byUser = await (await user.Client.GetAsync(
            $"/api/v1/device-management/users/{user.UserId}/devices")).ExpectAsync(HttpStatusCode.OK);
        Assert.Empty(bySite.EnumerateArray());
        Assert.Empty(byUser.EnumerateArray());
    }
}
