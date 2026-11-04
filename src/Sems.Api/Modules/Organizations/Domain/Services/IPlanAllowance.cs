namespace Sems.Api.Modules.Organizations.Domain.Services;

/// <summary>
/// Lo que organizaciones necesita saber del plan contratado.
/// </summary>
/// <remarks>
/// <para>Es un puerto declarado por el consumidor, como los de dispositivos y
/// energia. Organizaciones no tiene por que conocer que es una suscripcion, ni
/// un precio, ni una pasarela de pago: solo cuantos locales y
/// cuantos medidores le caben al cliente.</para>
/// </remarks>
public interface IPlanAllowance
{
    /// <summary>Cupos del plan vigente.</summary>
    /// <remarks>
    /// <c>int.MaxValue</c> representa "ilimitado" en ambos numeros.
    /// </remarks>
    public sealed record PlanAllowance(int SitesIncluded, int MetersPerSiteIncluded,
        string PlanName);

    /// <summary>
    /// Cupos del plan activo del usuario, o null si no tiene ninguno.
    /// </summary>
    /// <remarks>
    /// Devolver null y no un cupo de cero es deliberado: quien todavia no ha
    /// contratado nada esta probando el producto, y bloquearle el primer local
    /// le impide llegar a ver para que sirve.
    /// </remarks>
    Task<PlanAllowance?> AllowanceForAsync(Guid userId, CancellationToken ct = default);
}
