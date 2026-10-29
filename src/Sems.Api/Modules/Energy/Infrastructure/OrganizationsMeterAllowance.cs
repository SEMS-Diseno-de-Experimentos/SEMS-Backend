using Sems.Api.Modules.Energy.Domain.Services;
using Sems.Api.Modules.Organizations.Application;

namespace Sems.Api.Modules.Energy.Infrastructure;

/// <summary>
/// Resuelve <see cref="IMeterAllowance"/> preguntando a organizaciones.
/// </summary>
/// <remarks>
/// Delega en vez de recorrer las tablas por su cuenta: quien decide que plan
/// rige en una organizacion es organizaciones, y copiar esa regla aqui haria
/// que cambiarla en un sitio dejara el otro mintiendo.
/// </remarks>
public sealed class OrganizationsMeterAllowance : IMeterAllowance
{
    private readonly OrganizationQueryService _organizaciones;

    public OrganizationsMeterAllowance(OrganizationQueryService organizaciones) =>
        _organizaciones = organizaciones;

    public async Task<IMeterAllowance.MeterAllowance?> MeterAllowanceOfSiteAsync(Guid siteId,
        CancellationToken ct = default)
    {
        var cupo = await _organizaciones.MeterAllowanceOfSiteAsync(siteId, ct);
        return cupo is null
            ? null
            : new IMeterAllowance.MeterAllowance(cupo.MetersPerSiteIncluded, cupo.PlanName);
    }
}
