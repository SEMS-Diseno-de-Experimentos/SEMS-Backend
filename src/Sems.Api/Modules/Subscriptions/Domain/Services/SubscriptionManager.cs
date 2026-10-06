using Sems.Api.Modules.Subscriptions.Domain.Model;
using Sems.Api.Shared.Errors;

namespace Sems.Api.Modules.Subscriptions.Domain.Services;

/// <summary>
/// Servicio de dominio que concentra las reglas de transicion.
///
/// <para>No pertenecen a una sola entidad, asi que viven aqui en lugar de
/// dispersarse por la capa de aplicacion.</para>
/// </summary>
public sealed class SubscriptionManager
{
    public void EnsureCanCancel(SubscriptionStatus status)
    {
        if (status.IsFinal())
        {
            throw AppException.Conflict("subscription cannot be cancelled from current status");
        }
    }

    public void EnsureCanChangePlan(SubscriptionStatus status)
    {
        if (status.IsFinal())
        {
            throw AppException.Conflict("subscription cannot change plan from current status");
        }
    }
}
