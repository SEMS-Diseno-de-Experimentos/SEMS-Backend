using Microsoft.AspNetCore.Mvc;
using Sems.Api.Modules.Organizations.Application;
using Sems.Api.Shared.Errors;
using static Sems.Api.Modules.Organizations.Interfaces.OrganizationResources;

namespace Sems.Api.Modules.Organizations.Interfaces;

/// <summary>API REST de organizaciones y locales.</summary>
[ApiController]
[Route("api/v1/organizations")]
[Tags("Organizations")]
public sealed class OrganizationController : ControllerBase
{
    private readonly OrganizationCommandService _commands;
    private readonly OrganizationQueryService _queries;

    public OrganizationController(OrganizationCommandService commands,
        OrganizationQueryService queries)
    {
        _commands = commands;
        _queries = queries;
    }

    /// <summary>Da de alta una organizacion y deja a quien la crea como administrador.</summary>
    [HttpPost]
    public async Task<ActionResult<OrganizationResource>> Create(
        [FromBody] CreateOrganizationRequest request)
    {
        var (organizacion, _) = await _commands.RegisterAsync(request.LegalName, request.TradeName,
            request.TaxId, request.BusinessType, ParseId(request.OwnerUserId, "owner_user_id"));
        return StatusCode(StatusCodes.Status201Created, OrganizationResource.From(organizacion));
    }

    /// <summary>Organizaciones a las que pertenece una persona, con su papel.</summary>
    [HttpGet("/api/v1/users/{userId}/organizations")]
    public async Task<List<MyOrganizationResource>> Mine(string userId)
    {
        var mias = await _queries.ListMineAsync(ParseId(userId, "user_id"));
        return mias.Select(x => MyOrganizationResource.From(x.Organizacion, x.Vinculo)).ToList();
    }

    [HttpGet("{organizationId}")]
    public async Task<OrganizationResource> Get(string organizationId) =>
        OrganizationResource.From(await _queries.GetAsync(ParseId(organizationId, "organization_id")));

    [HttpPut("{organizationId}")]
    public async Task<OrganizationResource> Update(string organizationId,
        [FromBody] UpdateOrganizationRequest request) =>
        OrganizationResource.From(await _commands.UpdateAsync(
            ParseId(organizationId, "organization_id"), request.LegalName, request.TradeName,
            request.BusinessType));

    // ----------------------------------------------------------------- locales

    /// <summary>Da de alta un local en la organizacion.</summary>
    [HttpPost("{organizationId}/sites")]
    public async Task<ActionResult<SiteResource>> CreateSite(string organizationId,
        [FromBody] CreateSiteRequest request)
    {
        var local = await _commands.RegisterSiteAsync(ParseId(organizationId, "organization_id"),
            request.SiteCode, request.Name, request.Address, request.District, request.FloorAreaM2,
            request.ContractedPowerKw, request.TariffCategory);
        return StatusCode(StatusCodes.Status201Created, SiteResource.From(local));
    }

    /// <summary>Locales vigentes de la organizacion.</summary>
    [HttpGet("{organizationId}/sites")]
    public async Task<List<SiteResource>> ListSites(string organizationId)
    {
        var locales = await _queries.ListSitesAsync(ParseId(organizationId, "organization_id"));
        return locales.Select(SiteResource.From).ToList();
    }

    /// <summary>Personas con acceso a la organizacion.</summary>
    [HttpGet("{organizationId}/members")]
    public async Task<List<MembershipResource>> ListMembers(string organizationId)
    {
        var vinculos = await _queries.ListMembershipsAsync(ParseId(organizationId, "organization_id"));
        return vinculos.Select(MembershipResource.From).ToList();
    }

    /// <summary>Da acceso a una persona, o cambia el que ya tenia.</summary>
    [HttpPost("{organizationId}/members")]
    public async Task<ActionResult<MembershipResource>> GrantMembership(string organizationId,
        [FromBody] GrantMembershipRequest request)
    {
        var vinculo = await _commands.GrantMembershipAsync(
            ParseId(organizationId, "organization_id"), ParseId(request.UserId, "user_id"),
            request.Role, string.IsNullOrWhiteSpace(request.SiteId)
                ? null
                : ParseId(request.SiteId, "site_id"));
        return StatusCode(StatusCodes.Status201Created, MembershipResource.From(vinculo));
    }

    [HttpDelete("{organizationId}/members/{membershipId}")]
    public async Task<IActionResult> RevokeMembership(string organizationId, string membershipId)
    {
        await _commands.RevokeMembershipAsync(ParseId(membershipId, "membership_id"));
        return NoContent();
    }

    /// <summary>
    /// Convierte el identificador de la ruta o del cuerpo en Guid.
    /// </summary>
    /// <remarks>
    /// Sin esto, un identificador mal formado provoca una excepcion de formato
    /// que sale como 500, cuando en realidad la peticion viene mal: es un 400.
    /// </remarks>
    internal static Guid ParseId(string? value, string campo) =>
        Guid.TryParse(value, out var id) && id != Guid.Empty
            ? id
            : throw AppException.Validation($"{campo} is not a valid identifier");
}

/// <summary>API REST de locales y sus zonas.</summary>
[ApiController]
[Route("api/v1/sites")]
[Tags("Sites")]
public sealed class SiteController : ControllerBase
{
    private readonly OrganizationCommandService _commands;
    private readonly OrganizationQueryService _queries;

    public SiteController(OrganizationCommandService commands, OrganizationQueryService queries)
    {
        _commands = commands;
        _queries = queries;
    }

    [HttpGet("{siteId}")]
    public async Task<SiteResource> Get(string siteId) =>
        SiteResource.From(await _queries.GetSiteAsync(
            OrganizationController.ParseId(siteId, "site_id")));

    [HttpPut("{siteId}")]
    public async Task<SiteResource> Update(string siteId, [FromBody] UpdateSiteRequest request) =>
        SiteResource.From(await _commands.UpdateSiteAsync(
            OrganizationController.ParseId(siteId, "site_id"), request.Name, request.Address,
            request.District, request.FloorAreaM2, request.ContractedPowerKw,
            request.TariffCategory));

    [HttpDelete("{siteId}")]
    public async Task<IActionResult> Archive(string siteId)
    {
        await _commands.ArchiveSiteAsync(OrganizationController.ParseId(siteId, "site_id"));
        return NoContent();
    }

    /// <summary>Zonas vigentes del local.</summary>
    [HttpGet("{siteId}/zones")]
    public async Task<List<ZoneResource>> ListZones(string siteId)
    {
        var zonas = await _queries.ListZonesAsync(OrganizationController.ParseId(siteId, "site_id"));
        return zonas.Select(ZoneResource.From).ToList();
    }

    [HttpPost("{siteId}/zones")]
    public async Task<ActionResult<ZoneResource>> CreateZone(string siteId,
        [FromBody] CreateZoneRequest request)
    {
        var zona = await _commands.RegisterZoneAsync(
            OrganizationController.ParseId(siteId, "site_id"), request.Name, request.ZoneType,
            request.OperatesOffHours);
        return StatusCode(StatusCodes.Status201Created, ZoneResource.From(zona));
    }

    [HttpPut("/api/v1/zones/{zoneId}")]
    public async Task<ZoneResource> UpdateZone(string zoneId, [FromBody] UpdateZoneRequest request) =>
        ZoneResource.From(await _commands.UpdateZoneAsync(
            OrganizationController.ParseId(zoneId, "zone_id"), request.Name, request.ZoneType,
            request.OperatesOffHours));

    [HttpDelete("/api/v1/zones/{zoneId}")]
    public async Task<IActionResult> ArchiveZone(string zoneId)
    {
        await _commands.ArchiveZoneAsync(OrganizationController.ParseId(zoneId, "zone_id"));
        return NoContent();
    }
}
