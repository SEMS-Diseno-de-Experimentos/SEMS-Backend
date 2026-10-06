namespace Sems.Api.Modules.Analytics.Domain.Services;

/// <summary>Importe estimado de un recibo, desglosado.</summary>
public sealed record EstimatedBill(
    double EnergyCost,
    double PowerCost,
    double FixedCharge,
    double Total,
    string Currency,
    double TariffUsed);

/// <summary>
/// Lo unico que la analitica necesita de las tarifas.
/// </summary>
/// <remarks>
/// La analitica no tiene por que saber que existe una hora punta, un cargo por
/// exceso ni el IGV: solo necesita convertir un consumo previsto en un importe.
/// Ese calculo vive en el modulo de energia, que es su dueno, y aqui solo entra
/// el resultado.
/// </remarks>
public interface IBillCalculator
{
    EstimatedBill Estimate(string? tariffCategory, double kwhPeak, double kwhOffPeak,
        double maxDemandKw, double contractedPowerKw);
}
