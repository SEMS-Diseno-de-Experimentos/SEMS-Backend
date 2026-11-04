using Sems.Api.Modules.Organizations.Domain.Services;
using Sems.Api.Modules.Subscriptions.Domain.Model;
using Sems.Api.Modules.Subscriptions.Domain.Repositories;

namespace Sems.Api.Modules.Organizations.Infrastructure;

/// <summary>
/// Resuelve <see cref="IPlanAllowance"/> contra el modulo de suscripciones.
/// </summary>
/// <remarks>
/// La dependencia hacia el otro modulo vive en infraestructura, que es la capa
/// a la que le toca conocer el mundo exterior.
/// </remarks>
public sealed class SubscriptionsPlanAllowance : IPlanAllowance
{
    /// <summary>Codigo de la caracteristica que declara el cupo de locales.</summary>
    private const string SitesLimit = "SITES_LIMIT";

    /// <summary>Y el de la que declara cuantos medidores admite cada local.</summary>
    private const string MetersLimit = "DEVICES_PER_SITE_LIMIT";

    private readonly ISubscriptionRepository _subscriptions;
    private readonly IPlanRepository _plans;

    public SubscriptionsPlanAllowance(ISubscriptionRepository subscriptions, IPlanRepository plans)
    {
        _subscriptions = subscriptions;
        _plans = plans;
    }

    public async Task<IPlanAllowance.PlanAllowance?> AllowanceForAsync(Guid userId,
        CancellationToken ct = default)
    {
        var suscripciones = await _subscriptions.FindByUserIdAsync(userId.ToString(), ct);
        var activa = suscripciones.FirstOrDefault(s => s.Status == SubscriptionStatus.ACTIVE);
        if (activa is null)
        {
            return null;
        }

        var plan = await _plans.FindByIdAsync(activa.PlanId, ct);
        if (plan is null)
        {
            return null;
        }

        return new IPlanAllowance.PlanAllowance(
            Interpretar(Declarado(plan, SitesLimit)),
            Interpretar(Declarado(plan, MetersLimit)),
            plan.Name);
    }

    private static string? Declarado(SubscriptionPlan plan, string codigo) =>
        plan.PlanFeatures
            .FirstOrDefault(f => string.Equals(f.FeatureCode, codigo,
                StringComparison.OrdinalIgnoreCase))?.FeatureValue;

    /// <summary>
    /// Traduce el valor declarado en el plan a un numero de locales.
    /// </summary>
    /// <remarks>
    /// El catalogo escribe "ilimitado" en los planes sin tope. Un valor que no
    /// se entienda se trata como ilimitado y no como cero: ante la duda, es
    /// preferible dejar pasar un local de mas que bloquear a un cliente que
    /// esta pagando.
    /// </remarks>
    private static int Interpretar(string? declarado)
    {
        if (string.IsNullOrWhiteSpace(declarado))
        {
            return int.MaxValue;
        }
        return int.TryParse(declarado.Trim(), out var cupo) && cupo > 0 ? cupo : int.MaxValue;
    }
}
