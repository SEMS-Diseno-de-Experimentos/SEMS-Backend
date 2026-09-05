using Sems.Api.Modules.Energy.Domain.Model;

namespace Sems.Api.Modules.Energy.Domain.Services;

/// <summary>
/// Puerto hacia el proveedor externo de tarifas electricas.
///
/// <para>El dominio solo conoce esta interfaz. Hoy detras hay un adaptador
/// simulado; manana puede haber una integracion real sin tocar nada mas.</para>
/// </summary>
public interface IEnergyPricingProvider
{
    EnergyPrice CurrentPrice();
}
