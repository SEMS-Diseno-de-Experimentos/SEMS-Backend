using Sems.Api.Modules.Organizations.Domain.Model;
using Sems.Api.Modules.Organizations.Domain.Repositories;
using Sems.Api.Shared.Security;

namespace Sems.Api.Modules.Organizations.Infrastructure;

/// <summary>
/// Resuelve el alcance de un usuario con las membresias que ya guarda este
/// modulo.
/// </summary>
/// <remarks>
/// <para>El modelo de membresias existia desde el principio y solo lo usaba la
/// pantalla de personas. Esta clase es la que lo pone a decidir accesos, que es
/// para lo que se diseño: el <c>SiteId</c> nulo significa toda la cadena y uno
/// concreto significa ese local, y eso ya es exactamente la pregunta que hay
/// que contestar.</para>
///
/// <para>Las membresias del llamante se recuerdan por instancia. El servicio
/// esta registrado por peticion, asi que una peticion que comprueba varios
/// recursos las consulta una sola vez.</para>
/// </remarks>
public sealed class MembershipAccessPolicy : IAccessPolicy
{
    private readonly IMembershipRepository _memberships;
    private readonly ISiteRepository _sites;
    private readonly IZoneRepository _zones;

    private Guid _recordado = Guid.Empty;
    private List<Membership>? _vinculos;

    public MembershipAccessPolicy(IMembershipRepository memberships, ISiteRepository sites,
        IZoneRepository zones)
    {
        _memberships = memberships;
        _sites = sites;
        _zones = zones;
    }

    public async Task<bool> ReachesSiteAsync(Guid caller, Guid siteId,
        CancellationToken ct = default)
    {
        if (siteId == Guid.Empty)
        {
            return false;
        }
        var local = await _sites.FindByIdAsync(siteId, ct);
        if (local is null)
        {
            return false;
        }
        // Un vinculo sin local cubre la organizacion entera; uno con local
        // cubre solo ese.
        return (await VinculosDeAsync(caller, ct)).Any(v =>
            v.OrganizationId == local.OrganizationId
            && (v.SiteId is null || v.SiteId == siteId));
    }

    public async Task<bool> ReachesZoneAsync(Guid caller, Guid zoneId,
        CancellationToken ct = default)
    {
        var zona = await _zones.FindByIdAsync(zoneId, ct);
        return zona is not null && await ReachesSiteAsync(caller, zona.SiteId, ct);
    }

    public async Task<bool> ReachesOrganizationAsync(Guid caller, Guid organizationId,
        CancellationToken ct = default) =>
        organizationId != Guid.Empty
        && (await VinculosDeAsync(caller, ct)).Any(v => v.OrganizationId == organizationId);

    public async Task<bool> AdministersOrganizationAsync(Guid caller, Guid organizationId,
        CancellationToken ct = default) =>
        organizationId != Guid.Empty
        && (await VinculosDeAsync(caller, ct)).Any(v =>
            v.OrganizationId == organizationId && v.Role == MembershipRole.ORG_ADMIN);

    /// <summary>
    /// Vinculos vigentes del llamante.
    /// </summary>
    /// <remarks>
    /// Los que no estan activos se descartan aqui y no en cada pregunta: un
    /// vinculo revocado no da alcance a nada, y dejar esa comprobacion en
    /// manos de quien llama es pedir que algun dia se olvide.
    /// </remarks>
    private async Task<List<Membership>> VinculosDeAsync(Guid caller, CancellationToken ct)
    {
        if (_vinculos is not null && _recordado == caller)
        {
            return _vinculos;
        }
        var todos = await _memberships.FindByUserIdAsync(caller, ct);
        _vinculos = todos.Where(v => v.Status == OrgStatus.ACTIVE).ToList();
        _recordado = caller;
        return _vinculos;
    }
}
