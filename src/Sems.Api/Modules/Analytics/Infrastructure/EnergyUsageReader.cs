using Sems.Api.Modules.Analytics.Domain.Services;
using Sems.Api.Modules.Energy.Domain.Model;
using Sems.Api.Modules.Energy.Domain.Repositories;
using Sems.Api.Modules.Energy.Domain.Services;

namespace Sems.Api.Modules.Analytics.Infrastructure;

/// <summary>
/// Implementa el puerto de consumo de analitica leyendo el modulo de energia.
/// </summary>
/// <remarks>
/// Es el unico punto del modulo que conoce las lecturas y los medidores: el
/// motor de hallazgos trabaja solo con <see cref="IEnergyUsage.DailyUsage"/>.
/// Si manana el consumo viniera de una tabla agregada o de otro servicio, solo
/// cambia este archivo.
/// </remarks>
public sealed class EnergyUsageReader : IEnergyUsage
{
    private readonly IEnergyMeterRepository _meters;
    private readonly IEnergyReadingRepository _readings;
    private readonly IEnergyPricingProvider _pricing;
    private readonly ISiteCatalog _sites;

    public EnergyUsageReader(IEnergyMeterRepository meters, IEnergyReadingRepository readings,
        IEnergyPricingProvider pricing, ISiteCatalog sites)
    {
        _meters = meters;
        _readings = readings;
        _pricing = pricing;
        _sites = sites;
    }

    public async Task<List<IEnergyUsage.DailyUsage>> DailyUsageAsync(string userId, int days,
        CancellationToken ct = default)
    {
        var medidores = await _meters.FindByUserIdAsync(userId, ct);
        if (medidores.Count == 0)
        {
            return new List<IEnergyUsage.DailyUsage>();
        }

        var porId = medidores.ToDictionary(m => m.Id.ToString(), m => m);
        var desde = DateTime.UtcNow.Date.AddDays(-Math.Max(1, days));
        var lecturas = await _readings.FindByMetersAsync(porId.Keys.ToList(), desde, ct);
        if (lecturas.Count == 0)
        {
            return new List<IEnergyUsage.DailyUsage>();
        }

        // La exclusion de domingos es del suministro, no de la lectura: se
        // consulta una vez por local y no una vez por lectura.
        var excluyeDomingos = new Dictionary<Guid, bool>();
        foreach (var local in medidores.Select(m => m.SiteId).Where(s => s != Guid.Empty).Distinct())
        {
            var resumen = await _sites.SiteSummaryAsync(local, ct);
            excluyeDomingos[local] = resumen?.ExcludesSundaysFromPeak ?? false;
        }

        var acumulado = new Dictionary<(Guid Meter, DateOnly Day), Acumulado>();
        foreach (var lectura in lecturas)
        {
            if (!porId.TryGetValue(lectura.MeterId, out var medidor))
            {
                continue;
            }

            var dia = DateOnly.FromDateTime(HorarioPunta.AHoraLocal(lectura.Timestamp));
            var clave = (medidor.Id, dia);
            if (!acumulado.TryGetValue(clave, out var celda))
            {
                celda = acumulado[clave] = new Acumulado(medidor);
            }

            var excluye = medidor.SiteId != Guid.Empty
                          && excluyeDomingos.TryGetValue(medidor.SiteId, out var e) && e;
            celda.Suma(lectura, excluye);
        }

        return acumulado
            .Select(par => par.Value.Cerrar(par.Key.Day))
            .OrderBy(d => d.Day)
            .ToList();
    }

    public async Task<IEnergyUsage.SitePrices?> PricesOfAsync(Guid siteId,
        CancellationToken ct = default)
    {
        var local = await _sites.SiteSummaryAsync(siteId, ct);
        if (local is null)
        {
            return null;
        }

        var tarifa = _pricing.CurrentTariff(local.TariffCategory);
        return new IEnergyUsage.SitePrices(local.Name, local.TariffCategory,
            local.ContractedPowerKw, tarifa.Currency, tarifa.EnergiaPuntaPorKwh,
            tarifa.EnergiaFueraDePuntaPorKwh, tarifa.PotenciaPorKwMes);
    }

    public async Task<List<IEnergyUsage.ZoneName>> ZonesOfAsync(Guid siteId,
        CancellationToken ct = default) =>
        (await _sites.ActiveZonesOfAsync(siteId, ct))
        .Select(z => new IEnergyUsage.ZoneName(z.ZoneId, z.Name))
        .ToList();

    /// <summary>Suma de un medidor en un dia, separando la franja punta.</summary>
    private sealed class Acumulado
    {
        private readonly EnergyMeter _medidor;

        public Acumulado(EnergyMeter medidor) => _medidor = medidor;

        private double _kwh;
        private double _punta;
        private double _picoKw;

        public void Suma(EnergyReading lectura, bool excluyeDomingos)
        {
            _kwh += lectura.EnergyKwh;
            if (HorarioPunta.FranjaDe(lectura.Timestamp, excluyeDomingos) == FranjaHoraria.PUNTA)
            {
                _punta += lectura.EnergyKwh;
            }
            _picoKw = Math.Max(_picoKw, lectura.PowerWatts / 1000d);
        }

        public IEnergyUsage.DailyUsage Cerrar(DateOnly dia) =>
            new(_medidor.Id,
                _medidor.SiteId == Guid.Empty ? null : _medidor.SiteId,
                _medidor.ZoneId,
                dia,
                Math.Round(_kwh, 3),
                Math.Round(_punta, 3),
                Math.Round(_picoKw, 3));
    }
}
