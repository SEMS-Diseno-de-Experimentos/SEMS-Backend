using Microsoft.AspNetCore.Mvc;
using Sems.Api.Modules.Organizations.Application;
using Sems.Api.Shared.Http;
using Sems.Api.Shared.Security;
using Sems.Api.Shared.Errors;
using static Sems.Api.Modules.Organizations.Interfaces.OrganizationResources;

namespace Sems.Api.Modules.Organizations.Interfaces;

/// <summary>REST API for organizations and sites.</summary>
[ApiController]
[Route("api/v1/organizations")]
[Tags("Organizations")]
public sealed class OrganizationController : ControllerBase
{
    private readonly OrganizationCommandService _commands;
    private readonly OrganizationQueryService _queries;
    private readonly Sems.Api.Modules.Iam.Interfaces.Acl.IIamAcl _iam;
    private readonly IAccessPolicy _access;

    public OrganizationController(OrganizationCommandService commands,
        OrganizationQueryService queries, Sems.Api.Modules.Iam.Interfaces.Acl.IIamAcl iam,
        IAccessPolicy access)
    {
        _commands = commands;
        _queries = queries;
        _iam = iam;
        _access = access;
    }

    /// <summary>
    /// Pertenecer a la organizacion basta para leerla; cambiarla, no.
    /// </summary>
    /// <remarks>
    /// El modelo de membresias ya dice que el ORG_ADMIN manda en toda la cadena
    /// y que el supervisor se limita a su local. Aqui solo se aplica esa regla:
    /// dar de alta un local o repartir accesos es de la cadena, y leer quien
    /// trabaja en ella es de cualquiera que trabaje en ella.
    /// </remarks>
    private Task DebePertenecerAsync(string organizationId) =>
        _access.MustReachOrganizationAsync(User.CallerId(),
            ParseId(organizationId, "organization_id"));

    private Task DebeAdministrarAsync(string organizationId) =>
        _access.MustAdministerOrganizationAsync(User.CallerId(),
            ParseId(organizationId, "organization_id"));

    /// <summary>Registers an organization and makes its creator the administrator.</summary>
    [HttpPost]
    public async Task<ActionResult<OrganizationResource>> Create(
        [FromBody] CreateOrganizationRequest request)
    {
        // El alta hace administrador al dueño que venga en el cuerpo, asi que
        // poner ahi a otro seria darle una organizacion que no ha pedido, y a
        // uno mismo el mando sobre ella.
        var (organizacion, _) = await _commands.RegisterAsync(request.LegalName, request.TradeName,
            request.TaxId, request.BusinessType,
            User.MustBeSelf(request.OwnerUserId, "owner_user_id"));
        return StatusCode(StatusCodes.Status201Created, OrganizationResource.From(organizacion));
    }

    /// <summary>Organizations a person belongs to, with their role.</summary>
    [HttpGet("/api/v1/users/{userId}/organizations")]
    public async Task<List<MyOrganizationResource>> Mine(string userId)
    {
        var mias = await _queries.ListMineAsync(User.MustBeSelf(userId));
        return mias.Select(x => MyOrganizationResource.From(x.Organizacion, x.Vinculo)).ToList();
    }

    [HttpGet("{organizationId}")]
    public async Task<OrganizationResource> Get(string organizationId)
    {
        await DebePertenecerAsync(organizationId);
        return OrganizationResource.From(
            await _queries.GetAsync(ParseId(organizationId, "organization_id")));
    }

    [HttpPut("{organizationId}")]
    public async Task<OrganizationResource> Update(string organizationId,
        [FromBody] UpdateOrganizationRequest request)
    {
        await DebeAdministrarAsync(organizationId);
        return OrganizationResource.From(await _commands.UpdateAsync(
            ParseId(organizationId, "organization_id"), request.LegalName, request.TradeName,
            request.BusinessType));
    }

    // ----------------------------------------------------------------- locales

    /// <summary>Registers a site in the organization.</summary>
    [HttpPost("{organizationId}/sites")]
    public async Task<ActionResult<SiteResource>> CreateSite(string organizationId,
        [FromBody] CreateSiteRequest request)
    {
        await DebeAdministrarAsync(organizationId);
        var local = await _commands.RegisterSiteAsync(ParseId(organizationId, "organization_id"),
            request.SiteCode, request.Name, request.Address, request.District, request.FloorAreaM2,
            request.ContractedPowerKw, request.TariffCategory,
            request.ExcludesSundaysFromPeak ?? false);
        return StatusCode(StatusCodes.Status201Created, SiteResource.From(local));
    }

    /// <summary>Active sites of the organization.</summary>
    [HttpGet("{organizationId}/sites")]
    public async Task<List<SiteResource>> ListSites(string organizationId)
    {
        await DebePertenecerAsync(organizationId);
        var locales = await _queries.ListSitesAsync(ParseId(organizationId, "organization_id"));
        return locales.Select(SiteResource.From).ToList();
    }

    /// <summary>People with access to the organization.</summary>
    [HttpGet("{organizationId}/members")]
    public async Task<List<MembershipResource>> ListMembers(string organizationId,
        CancellationToken ct)
    {
        // Esta ruta publica los correos de los companeros, asi que la ve quien
        // trabaja en la organizacion y nadie mas.
        await DebePertenecerAsync(organizationId);
        var vinculos = await _queries.ListMembershipsAsync(ParseId(organizationId, "organization_id"));
        var correos = await _iam.EmailsOfAsync(vinculos.Select(v => v.UserId).Distinct().ToList(), ct);
        return vinculos
            .Select(v => MembershipResource.From(v,
                correos.TryGetValue(v.UserId, out var correo) ? correo : null))
            .ToList();
    }

    /// <summary>Grants access to a person, or changes their existing access.</summary>
    [HttpPost("{organizationId}/members")]
    public async Task<ActionResult<MembershipResource>> GrantMembership(string organizationId,
        [FromBody] GrantMembershipRequest request, CancellationToken ct)
    {
        await DebeAdministrarAsync(organizationId);
        var usuario = await ResolverUsuarioAsync(request, ct);
        var vinculo = await _commands.GrantMembershipAsync(
            ParseId(organizationId, "organization_id"), usuario,
            request.Role, string.IsNullOrWhiteSpace(request.SiteId)
                ? null
                : ParseId(request.SiteId, "site_id"), ct);
        return StatusCode(StatusCodes.Status201Created,
            MembershipResource.From(vinculo, await _iam.EmailOfAsync(usuario, ct)));
    }

    /// <summary>
    /// Resuelve a quien se le da acceso, por correo o por identificador.
    /// </summary>
    /// <remarks>
    /// Un correo que no corresponde a ninguna cuenta responde 404 y no 400: la
    /// peticion esta bien formada, lo que falta es que esa persona se registre.
    /// </remarks>
    private async Task<Guid> ResolverUsuarioAsync(GrantMembershipRequest request,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            return ParseId(request.UserId, "user_id");
        }
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw AppException.Validation("either email or user_id is required");
        }
        return await _iam.UserIdOfAsync(request.Email, ct)
               ?? throw AppException.NotFound($"no account is registered with '{request.Email}'");
    }

    [HttpDelete("{organizationId}/members/{membershipId}")]
    public async Task<IActionResult> RevokeMembership(string organizationId, string membershipId)
    {
        await DebeAdministrarAsync(organizationId);

        // Que el vinculo sea de la organizacion de la ruta no es un detalle: sin
        // esta comprobacion, el administrador de una cadena podria revocar el
        // acceso de alguien de otra pasando su propio identificador y el
        // vinculo ajeno.
        var vinculo = await _queries.GetMembershipAsync(ParseId(membershipId, "membership_id"));
        if (vinculo.OrganizationId != ParseId(organizationId, "organization_id"))
        {
            throw AppException.Forbidden("the membership does not belong to that organization");
        }

        await _commands.RevokeMembershipAsync(vinculo.MembershipId);
        return NoContent();
    }

    /// <summary>
    /// Converts the identifier from the route or the body into a Guid.
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

/// <summary>REST API for sites and their zones.</summary>
[ApiController]
[Route("api/v1/sites")]
[Tags("Sites")]
public sealed class SiteController : ControllerBase
{
    private readonly OrganizationCommandService _commands;
    private readonly OrganizationQueryService _queries;

    private readonly IAccessPolicy _access;

    public SiteController(OrganizationCommandService commands, OrganizationQueryService queries,
        IAccessPolicy access)
    {
        _commands = commands;
        _queries = queries;
        _access = access;
    }

    /// <summary>
    /// Alcanzar el local basta para leerlo y para organizar sus zonas.
    /// </summary>
    /// <remarks>
    /// El alcance se comprueba antes de cargar nada: un local que no existe
    /// responde lo mismo que uno ajeno, y asi nadie averigua que
    /// identificadores existen pidiendolos uno a uno.
    /// </remarks>
    private Task DebeAlcanzarAsync(Guid siteId) =>
        _access.MustReachSiteAsync(User.CallerId(), siteId);

    /// <summary>
    /// Cambiar la potencia contratada o la tarifa de un local es del contrato
    /// de la cadena, no del dia a dia de la tienda: eso lo decide el
    /// administrador de la organizacion.
    /// </summary>
    private async Task DebeAdministrarElLocalAsync(Guid siteId)
    {
        await DebeAlcanzarAsync(siteId);
        var local = await _queries.GetSiteAsync(siteId);
        await _access.MustAdministerOrganizationAsync(User.CallerId(), local.OrganizationId);
    }

    [HttpGet("{siteId}")]
    public async Task<SiteResource> Get(string siteId)
    {
        var id = OrganizationController.ParseId(siteId, "site_id");
        await DebeAlcanzarAsync(id);
        return SiteResource.From(await _queries.GetSiteAsync(id));
    }

    [HttpPut("{siteId}")]
    public async Task<SiteResource> Update(string siteId, [FromBody] UpdateSiteRequest request)
    {
        var id = OrganizationController.ParseId(siteId, "site_id");
        await DebeAdministrarElLocalAsync(id);
        return SiteResource.From(await _commands.UpdateSiteAsync(id, request.Name, request.Address,
            request.District, request.FloorAreaM2, request.ContractedPowerKw,
            request.TariffCategory, request.ExcludesSundaysFromPeak ?? false));
    }

    [HttpDelete("{siteId}")]
    public async Task<IActionResult> Archive(string siteId)
    {
        var id = OrganizationController.ParseId(siteId, "site_id");
        await DebeAdministrarElLocalAsync(id);
        await _commands.ArchiveSiteAsync(id);
        return NoContent();
    }

    /// <summary>Active zones of the site.</summary>
    [HttpGet("{siteId}/zones")]
    public async Task<List<ZoneResource>> ListZones(string siteId)
    {
        var id = OrganizationController.ParseId(siteId, "site_id");
        await DebeAlcanzarAsync(id);
        var zonas = await _queries.ListZonesAsync(id);
        return zonas.Select(ZoneResource.From).ToList();
    }

    [HttpPost("{siteId}/zones")]
    public async Task<ActionResult<ZoneResource>> CreateZone(string siteId,
        [FromBody] CreateZoneRequest request)
    {
        var id = OrganizationController.ParseId(siteId, "site_id");

        // Repartir el local en zonas es trabajo de la tienda, no del contrato:
        // el supervisor de ese local puede hacerlo.
        await DebeAlcanzarAsync(id);
        var zona = await _commands.RegisterZoneAsync(id, request.Name, request.ZoneType,
            request.OperatesOffHours);
        return StatusCode(StatusCodes.Status201Created, ZoneResource.From(zona));
    }

    [HttpPut("/api/v1/zones/{zoneId}")]
    public async Task<ZoneResource> UpdateZone(string zoneId, [FromBody] UpdateZoneRequest request)
    {
        var id = OrganizationController.ParseId(zoneId, "zone_id");
        await _access.MustReachZoneAsync(User.CallerId(), id);
        return ZoneResource.From(await _commands.UpdateZoneAsync(id, request.Name, request.ZoneType,
            request.OperatesOffHours));
    }

    [HttpDelete("/api/v1/zones/{zoneId}")]
    public async Task<IActionResult> ArchiveZone(string zoneId)
    {
        var id = OrganizationController.ParseId(zoneId, "zone_id");
        await _access.MustReachZoneAsync(User.CallerId(), id);
        await _commands.ArchiveZoneAsync(id);
        return NoContent();
    }
}
