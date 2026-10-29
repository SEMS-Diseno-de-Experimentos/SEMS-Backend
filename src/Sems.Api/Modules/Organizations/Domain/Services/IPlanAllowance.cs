namespace Sems.Api.Modules.Organizations.Domain.Services;

/// <summary>
/// Lo que organizaciones necesita saber del plan contratado.
/// </summary>
/// <remarks>
/// <para>Es un puerto declarado por el consumidor, como los de dispositivos y
/// energia. Organizaciones no tiene por que conocer que es una suscripcion, ni
/// un precio, ni una pasarela de pago: solo cuantos locales le caben al
/// cliente.</para>
/// </remarks>
public interface IPlanAllowance
{
    /// <summary>Cupo de locales del plan vigente.</summary>
    /// <remarks>
    /// En <c>SitesIncluded</c>, <c>int.MaxValue</c> representa "ilimitado".
    /// </remarks>
    public sealed record SiteAllowance(int SitesIncluded, string PlanName);

    /// <summary>
    /// Cupo del plan activo del usuario, o null si no tiene ninguno.
    /// </summary>
    /// <remarks>
    /// Devolver null y no un cupo de cero es deliberado: quien todavia no ha
    /// contratado nada esta probando el producto, y bloquearle el primer local
    /// le impide llegar a ver para que sirve.
    /// </remarks>
    Task<SiteAllowance?> SiteAllowanceForAsync(Guid userId, CancellationToken ct = default);
}
