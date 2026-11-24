using Sems.Api.Modules.Energy.Domain.Model;

namespace Sems.Api.Modules.Energy.Domain.Services;

/// <summary>
/// Resolucion tolerante de la tarifa de un local.
/// </summary>
public static class EnergyPricingProviderExtensions
{
    /// <summary>
    /// Tarifa de la categoria, o <c>null</c> si no hay categoria o no se
    /// reconoce.
    /// </summary>
    /// <remarks>
    /// Un local sin tarifa reconocida sigue teniendo consumo que mostrar; lo
    /// que no se puede es ponerle precio. Por eso aqui no se propaga el error:
    /// quien llama decide que hacer con el null, y lo que hacen todos es
    /// enseñar los kWh sin el importe.
    /// </remarks>
    public static CommercialTariff? TariffOrNull(this IEnergyPricingProvider pricing,
        string? tariffCategory)
    {
        if (string.IsNullOrWhiteSpace(tariffCategory))
        {
            return null;
        }

        try
        {
            return pricing.CurrentTariff(tariffCategory);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
