namespace Sems.Api.Modules.Energy.Domain.Services;

/// <summary>
/// Cuantos medidores admite un local segun el plan contratado.
/// </summary>
/// <remarks>
/// <para>Otro puerto declarado por el consumidor, como <see cref="ISiteCatalog"/>.
/// Energia no sabe que existe una suscripcion ni quien administra la
/// organizacion del local: solo pregunta cuantos medidores le caben.</para>
///
/// <para>El catalogo vende "10 / 50 / ilimitado medidores por local" y hasta
/// ahora nadie lo miraba, de modo que un cliente del plan mas barato podia
/// instalar los que quisiera. Igual que con los locales, el cupo es lo que
/// distingue a un plan de otro.</para>
/// </remarks>
public interface IMeterAllowance
{
    /// <summary>Cupo de medidores del local, o null si no hay nada que exigir.</summary>
    public sealed record MeterAllowance(int MetersIncluded, string PlanName);

    Task<MeterAllowance?> MeterAllowanceOfSiteAsync(Guid siteId, CancellationToken ct = default);
}
