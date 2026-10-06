using System.Net;
using Sems.Api.TestSupport;

namespace Sems.Api.IntegrationTests.Organizations;

/// <summary>
/// Organizaciones, locales, zonas y vinculos. Contrato en snake_case.
/// </summary>
public class OrganizationEndpointsTests : IClassFixture<SemsApiFactory>
{
    private readonly SemsApiFactory _factory;

    public OrganizationEndpointsTests(SemsApiFactory factory) => _factory = factory;

    private async Task<(TestUser Admin, Guid OrganizationId)> AdminWithOrganizationAsync()
    {
        var admin = await _factory.CreateUserAsync();
        var organization = await admin.Client.CreateOrganizationAsync(admin.UserId);
        return (admin, organization.Id("organization_id"));
    }

    // ------------------------------------------------------------ organizacion

    [Fact]
    public async Task Create_ValidTaxId_Returns201AndCreatorIsOrgAdmin()
    {
        var admin = await _factory.CreateUserAsync();

        var response = await admin.Client.PostOrganizationAsync(admin.UserId, "20601234567",
            "Minimarket Los Andes S.A.C.");

        var organization = await response.ExpectAsync(HttpStatusCode.Created);
        Assert.Equal("20601234567", organization.Text("tax_id"));
        Assert.Equal("ACTIVE", organization.Text("status"));

        var mine = await (await admin.Client.GetAsync($"/api/v1/users/{admin.UserId}/organizations"))
            .ExpectAsync(HttpStatusCode.OK);
        var membership = Assert.Single(mine.EnumerateArray());
        Assert.Equal("ORG_ADMIN", membership.Text("role"));
        Assert.Equal(organization.Text("organization_id"),
            membership.GetProperty("organization").Text("organization_id"));
    }

    [Fact]
    public async Task Create_RegisteredTaxId_Returns409()
    {
        var admin = await _factory.CreateUserAsync();
        var taxId = ApiRequests.NewTaxId();
        await admin.Client.CreateOrganizationAsync(admin.UserId, taxId);

        var response = await admin.Client.PostOrganizationAsync(admin.UserId, taxId, "Copycat S.A.C.");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("2060123456")]
    [InlineData("206012345678")]
    [InlineData("2060123456A")]
    public async Task Create_MalformedTaxId_Returns400(string taxId)
    {
        var admin = await _factory.CreateUserAsync();

        var response = await admin.Client.PostOrganizationAsync(admin.UserId, taxId, "Bodega Central");

        var body = await response.ExpectAsync(HttpStatusCode.BadRequest);
        Assert.Equal("tax_id must be 11 digits", body.Text("message"));
    }

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.PostOrganizationAsync(Guid.NewGuid(), ApiRequests.NewTaxId());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_UnknownOrganization_Returns404()
    {
        var user = await _factory.CreateUserAsync();

        var response = await user.Client.GetAsync($"/api/v1/organizations/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_IdentifierThatIsNotUuid_Returns400()
    {
        var user = await _factory.CreateUserAsync();

        var response = await user.Client.GetAsync("/api/v1/organizations/not-a-uuid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------------------ locales

    [Theory]
    [InlineData("BT5B", false)]
    [InlineData("BT3", true)]
    [InlineData("BT4", true)]
    [InlineData("MT2", true)]
    [InlineData("MT3", true)]
    public async Task CreateSite_EachTariffCategory_Returns201ChargingDemandOnlyOutsideBt5b(
        string tariffCategory, bool chargesForDemand)
    {
        var (admin, organizationId) = await AdminWithOrganizationAsync();

        var response = await admin.Client.PostSiteAsync(organizationId, "T-001", 120m, tariffCategory);

        var site = await response.ExpectAsync(HttpStatusCode.Created);
        Assert.Equal(tariffCategory, site.Text("tariff_category"));
        Assert.Equal(chargesForDemand, site.GetProperty("charges_for_demand").GetBoolean());
        Assert.False(site.GetProperty("excludes_sundays_from_peak").GetBoolean());
    }

    [Fact]
    public async Task CreateSite_RepeatedCodeInTheSameOrganization_Returns409()
    {
        var (admin, organizationId) = await AdminWithOrganizationAsync();
        await admin.Client.CreateSiteAsync(organizationId, "T-001");

        var response = await admin.Client.PostSiteAsync(organizationId, "t-001");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateSite_SameCodeInAnotherOrganization_Returns201()
    {
        var (admin, organizationId) = await AdminWithOrganizationAsync();
        await admin.Client.CreateSiteAsync(organizationId, "T-001");
        var other = await admin.Client.CreateOrganizationAsync(admin.UserId);

        var response = await admin.Client.PostSiteAsync(other.Id("organization_id"), "T-001");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task CreateSite_NonPositiveContractedPower_Returns400(decimal contractedPowerKw)
    {
        var (admin, organizationId) = await AdminWithOrganizationAsync();

        var response = await admin.Client.PostSiteAsync(organizationId, "T-009", contractedPowerKw);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSite_UnknownTariffCategory_Returns400()
    {
        var (admin, organizationId) = await AdminWithOrganizationAsync();

        var response = await admin.Client.PostSiteAsync(organizationId, "T-009", 120m, "BT6");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ArchiveSite_ExistingSite_Returns204AndLeavesTheListingButStaysQueryable()
    {
        var (admin, organizationId) = await AdminWithOrganizationAsync();
        await admin.Client.CreateSiteAsync(organizationId, "T-001");
        var archived = await admin.Client.CreateSiteAsync(organizationId, "T-002");
        var archivedId = archived.Id("site_id");

        var response = await admin.Client.DeleteAsync($"/api/v1/sites/{archivedId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var listing = await (await admin.Client.GetAsync($"/api/v1/organizations/{organizationId}/sites"))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.Equal(new[] { "T-001" }, listing.EnumerateArray().Select(s => s.Text("site_code")));
        var byId = await (await admin.Client.GetAsync($"/api/v1/sites/{archivedId}"))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.Equal("ARCHIVED", byId.Text("status"));
    }

    // -------------------------------------------------------------------- zonas

    [Fact]
    public async Task CreateZone_ColdStorage_Returns201OperatingOffHours()
    {
        var (admin, organizationId) = await AdminWithOrganizationAsync();
        var site = await admin.Client.CreateSiteAsync(organizationId, "T-001");

        var response = await admin.Client.PostZoneAsync(site.Id("site_id"), "Cold rooms", "COLD_STORAGE");

        var zone = await response.ExpectAsync(HttpStatusCode.Created);
        Assert.True(zone.GetProperty("operates_off_hours").GetBoolean());
        Assert.Equal(site.Text("site_id"), zone.Text("site_id"));
    }

    [Fact]
    public async Task CreateZone_ArchivedSite_Returns409()
    {
        var (admin, organizationId) = await AdminWithOrganizationAsync();
        var site = await admin.Client.CreateSiteAsync(organizationId, "T-001");
        await admin.Client.DeleteAsync($"/api/v1/sites/{site.Text("site_id")}");

        var response = await admin.Client.PostZoneAsync(site.Id("site_id"), "Cold rooms", "COLD_STORAGE");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---------------------------------------------------------------- vinculos

    [Fact]
    public async Task GrantMembership_SupervisorWithoutSite_Returns400()
    {
        var (admin, organizationId) = await AdminWithOrganizationAsync();
        var supervisor = await _factory.CreateUserAsync();

        var response = await admin.Client.PostJsonAsync(
            $"/api/v1/organizations/{organizationId}/members",
            new { user_id = supervisor.UserId.ToString(), role = "SUPERVISOR", site_id = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RevokeMembership_LastAdministrator_Returns409()
    {
        var (admin, organizationId) = await AdminWithOrganizationAsync();
        var members = await (await admin.Client.GetAsync($"/api/v1/organizations/{organizationId}/members"))
            .ExpectAsync(HttpStatusCode.OK);
        var adminMembership = Assert.Single(members.EnumerateArray());

        var response = await admin.Client.DeleteAsync(
            $"/api/v1/organizations/{organizationId}/members/{adminMembership.Text("membership_id")}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GrantMembership_AgainAfterRevoking_Returns201()
    {
        var (admin, organizationId) = await AdminWithOrganizationAsync();
        var operatorUser = await _factory.CreateUserAsync();
        var membersUrl = $"/api/v1/organizations/{organizationId}/members";
        var granted = await (await admin.Client.PostJsonAsync(membersUrl,
                new { user_id = operatorUser.UserId.ToString(), role = "OPERATOR" }))
            .ExpectAsync(HttpStatusCode.Created);
        await (await admin.Client.DeleteAsync($"{membersUrl}/{granted.Text("membership_id")}"))
            .ExpectAsync(HttpStatusCode.NoContent);

        var response = await admin.Client.PostJsonAsync(membersUrl,
            new { user_id = operatorUser.UserId.ToString(), role = "OPERATOR" });

        var regranted = await response.ExpectAsync(HttpStatusCode.Created);
        Assert.Equal("ACTIVE", regranted.Text("status"));
        Assert.Equal("OPERATOR", regranted.Text("role"));
    }
}
