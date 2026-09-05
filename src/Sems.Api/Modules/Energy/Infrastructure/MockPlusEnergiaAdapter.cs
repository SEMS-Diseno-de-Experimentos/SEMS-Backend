using Sems.Api.Modules.Energy.Domain.Model;
using Sems.Api.Modules.Energy.Domain.Services;

namespace Sems.Api.Modules.Energy.Infrastructure;

/// <summary>
/// Adaptador simulado del proveedor ficticio Plus Energia.
///
/// <para>Porta el comportamiento exacto del adaptador en Python: la semilla del
/// generador es la fecha del dia, de modo que el precio es aleatorio entre
/// usuarios pero <b>estable durante toda la jornada</b>. Sin esa semilla la
/// tarifa cambiaria en cada peticion y los costes mostrados al usuario
/// bailarian.</para>
/// </summary>
public sealed class MockPlusEnergiaAdapter : IEnergyPricingProvider
{
    public EnergyPrice CurrentPrice()
    {
        var now = DateTime.UtcNow;
        var rng = new Random(now.ToString("yyyyMMdd").GetHashCode());
        var price = Math.Round(0.68 + rng.NextDouble() * (0.92 - 0.68), 2);
        return new EnergyPrice("Plus Energia", price, "PEN", now);
    }
}
