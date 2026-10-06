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

    /// <summary>
    /// Tarifa comercial simulada para la categoria pedida.
    /// </summary>
    /// <remarks>
    /// <para>Los importes son <b>referenciales</b>, del mismo orden de magnitud
    /// que los del pliego tarifario, pero no son los oficiales: este adaptador
    /// simula al proveedor. Cuando se integre uno real, se sustituye esta clase
    /// y no cambia nada mas, que es el motivo de que exista el puerto.</para>
    ///
    /// <para>BT5B es el caso aparte: es la tarifa sin cargo por potencia, la
    /// misma que tendria una vivienda. Se mantiene porque un local pequeno puede
    /// estar en ella, y devolver cargos de potencia en cero es mas honesto que
    /// negarse a responder.</para>
    /// </remarks>
    public CommercialTariff CurrentTariff(string? tariffCategory)
    {
        var categoria = (tariffCategory ?? "MT2").Trim().ToUpperInvariant();
        var ahora = DateTime.UtcNow;

        // Sin cargo por potencia: el local paga solo energia, a precio unico.
        if (categoria == "BT5B")
        {
            var plano = (decimal)CurrentPrice().PricePerKwh;
            return new CommercialTariff("Plus Energia", categoria, "PEN",
                EnergiaPuntaPorKwh: plano,
                EnergiaFueraDePuntaPorKwh: plano,
                PotenciaPorKwMes: 0m,
                ExcesoDePotenciaPorKwMes: 0m,
                CargoFijoMensual: 4.50m,
                Igv: 0.18m,
                Timestamp: ahora);
        }

        // Media tension frente a baja tension: la media es mas barata en energia
        // y mas cara en potencia, que es justo lo que hace que a un local grande
        // le compense y a uno pequeno no.
        var esMediaTension = categoria.StartsWith("MT", StringComparison.Ordinal);

        return new CommercialTariff("Plus Energia", categoria, "PEN",
            EnergiaPuntaPorKwh: esMediaTension ? 0.2810m : 0.3120m,
            EnergiaFueraDePuntaPorKwh: esMediaTension ? 0.2395m : 0.2680m,
            PotenciaPorKwMes: esMediaTension ? 58.40m : 42.10m,
            // El exceso sobre lo contratado se penaliza. Que sea mas caro que la
            // potencia normal no es un adorno: es lo que convierte un pico
            // puntual en una linea visible de la factura.
            ExcesoDePotenciaPorKwMes: esMediaTension ? 87.60m : 63.15m,
            CargoFijoMensual: esMediaTension ? 12.80m : 8.40m,
            Igv: 0.18m,
            Timestamp: ahora);
    }
}
