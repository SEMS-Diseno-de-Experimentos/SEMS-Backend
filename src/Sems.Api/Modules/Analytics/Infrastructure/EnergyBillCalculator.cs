using Sems.Api.Modules.Analytics.Domain.Services;
using Sems.Api.Modules.Energy.Domain.Services;

namespace Sems.Api.Modules.Analytics.Infrastructure;

/// <summary>
/// Resuelve <see cref="IBillCalculator"/> delegando en el modulo de energia.
/// </summary>
/// <remarks>
/// La dependencia entre modulos queda aqui, en infraestructura, y en una sola
/// direccion: analitica conoce a energia, nunca al reves.
/// </remarks>
public sealed class EnergyBillCalculator : IBillCalculator
{
    private readonly IEnergyPricingProvider _pricing;

    public EnergyBillCalculator(IEnergyPricingProvider pricing) => _pricing = pricing;

    public EstimatedBill Estimate(string? tariffCategory, double kwhPeak, double kwhOffPeak,
        double maxDemandKw, double contractedPowerKw)
    {
        var tarifa = _pricing.CurrentTariff(tariffCategory);
        var desglose = tarifa.Calcular((decimal)kwhPeak, (decimal)kwhOffPeak,
            (decimal)maxDemandKw, (decimal)contractedPowerKw);

        return new EstimatedBill(
            EnergyCost: (double)desglose.CostoEnergia,
            PowerCost: (double)desglose.CostoPotencia,
            FixedCharge: (double)desglose.CargoFijo,
            Total: (double)desglose.Total,
            Currency: desglose.Currency,
            // Se guarda el precio de la energia fuera de punta como "tarifa
            // usada" porque es la que aplica a la mayor parte del consumo. El
            // desglose completo va en los campos de la prediccion.
            TariffUsed: (double)tarifa.EnergiaFueraDePuntaPorKwh);
    }
}
