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

    /// <summary>
    /// Tarifa comercial vigente para una categoria del pliego.
    /// </summary>
    /// <remarks>
    /// La categoria llega como texto y no como enum de otro modulo a proposito:
    /// energia no debe depender del bounded context de organizaciones. Es quien
    /// llama el que traduce, igual que ocurre con el tipo de dispositivo.
    /// </remarks>
    CommercialTariff CurrentTariff(string? tariffCategory);
}
