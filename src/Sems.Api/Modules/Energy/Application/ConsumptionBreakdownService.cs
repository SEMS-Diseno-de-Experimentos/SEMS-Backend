using Sems.Api.Modules.Energy.Domain.Model;
using Sems.Api.Modules.Energy.Domain.Repositories;
using Sems.Api.Modules.Energy.Domain.Services;
using Sems.Api.Shared.Errors;

namespace Sems.Api.Modules.Energy.Application;

/// <summary>
/// Reparto del consumo: por zona dentro de un local y entre los locales de una
/// organizacion.
/// </summary>
/// <remarks>
/// <para>Las dos preguntas se contestan igual —agrupar lecturas y sumarlas—
/// pero responden a cosas distintas. Dentro de un local interesa <i>donde</i>
/// se gasta: las camaras de frio y la climatizacion suelen llevarse la mayor
/// parte, y verlo en reparto es lo que permite decidir donde intervenir. Entre
/// locales interesa <i>cual</i> gasta de mas, y ahi el total engana: un local
/// grande consume mas que uno pequeno sin que eso signifique nada. Por eso la
/// comparacion entre locales da tambien el consumo por kW contratado, que si es
/// comparable.</para>
/// </remarks>
public sealed class ConsumptionBreakdownService
{
    /// <summary>Ventana por defecto: un mes, que es el periodo del recibo.</summary>
    public const int DiasPorDefecto = 30;
    private const int DiasMaximo = 365;

    private readonly IEnergyMeterRepository _meters;
    private readonly IEnergyReadingRepository _readings;
    private readonly IEnergyPricingProvider _pricing;
    private readonly ISiteCatalog _sites;

    public ConsumptionBreakdownService(IEnergyMeterRepository meters,
        IEnergyReadingRepository readings, IEnergyPricingProvider pricing, ISiteCatalog sites)
    {
        _meters = meters;
        _readings = readings;
        _pricing = pricing;
        _sites = sites;
    }

    public sealed record ZoneShare(Guid? ZoneId, string? ZoneName, string? ZoneType, int MeterCount,
        double Kwh, double SharePercent, decimal? EstimatedEnergyCost);

    public sealed record ZoneBreakdown(Guid SiteId, string SiteName, int Days, DateTime From,
        DateTime To, double TotalKwh, string Currency, List<ZoneShare> Zones, ZoneShare Unassigned);

    public sealed record SiteShare(Guid SiteId, string SiteCode, string SiteName,
        decimal ContractedPowerKw, string? TariffCategory, int MeterCount, double Kwh,
        double SharePercent, decimal? EstimatedEnergyCost, double? KwhPerContractedKw,
        double PeakDemandKw);

    public sealed record SiteComparison(Guid OrganizationId, int Days, DateTime From, DateTime To,
        double TotalKwh, string Currency, List<SiteShare> Sites);

    public sealed record CurrentConsumption(Guid SiteId, string SiteName, int MeterCount,
        double PowerKw, decimal ContractedPowerKw, double? OccupancyPercent, DateTime? MeasuredAt);

    /// <summary>Consumo de un local repartido entre sus zonas (US26).</summary>
    public async Task<ZoneBreakdown> ByZoneAsync(Guid siteId, int days,
        CancellationToken ct = default)
    {
        var local = await _sites.SiteSummaryAsync(siteId, ct)
                    ?? throw AppException.NotFound($"site '{siteId}' not found");
        var desde = Desde(days);

        var medidores = await _meters.FindBySiteIdAsync(siteId, ct);
        var zonaDelMedidor = medidores.ToDictionary(m => m.Id.ToString(), m => m.ZoneId);

        var porZona = new Dictionary<Guid, Acumulado>();
        var sinZona = new Acumulado();
        foreach (var lectura in await _readings.FindByMetersAsync(zonaDelMedidor.Keys.ToList(), desde, ct))
        {
            var zona = zonaDelMedidor.TryGetValue(lectura.MeterId, out var z) ? z : null;
            var acumulado = zona is null
                ? sinZona
                : porZona.TryGetValue(zona.Value, out var existente)
                    ? existente
                    : porZona[zona.Value] = new Acumulado();
            acumulado.Suma(lectura, local.ExcludesSundaysFromPeak);
        }

        var medidoresPorZona = medidores.Where(m => m.ZoneId is not null)
            .GroupBy(m => m.ZoneId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        var medidoresSinZona = medidores.Count(m => m.ZoneId is null);

        var tarifa = TarifaDe(local.TariffCategory);
        var totalKwh = porZona.Values.Sum(a => a.Kwh) + sinZona.Kwh;

        var zonas = new List<ZoneShare>();
        foreach (var zona in await _sites.ActiveZonesOfAsync(siteId, ct))
        {
            var consumo = porZona.TryGetValue(zona.ZoneId, out var a) ? a : new Acumulado();
            zonas.Add(new ZoneShare(zona.ZoneId, zona.Name, zona.ZoneType,
                medidoresPorZona.TryGetValue(zona.ZoneId, out var n) ? n : 0,
                Redondea(consumo.Kwh), Porcentaje(consumo.Kwh, totalKwh), Coste(consumo, tarifa)));
        }
        zonas = zonas.OrderByDescending(z => z.Kwh).ToList();

        // Los medidores que aun no estan asignados a una zona no se esconden: si
        // se omitieran, los porcentajes no sumarian cien y el reparto mentiria.
        var noAsignado = new ZoneShare(null, null, null, medidoresSinZona, Redondea(sinZona.Kwh),
            Porcentaje(sinZona.Kwh, totalKwh), Coste(sinZona, tarifa));

        return new ZoneBreakdown(siteId, local.Name, Dias(days), desde, DateTime.UtcNow,
            Redondea(totalKwh), Moneda(tarifa), zonas, noAsignado);
    }

    /// <summary>Consumo de cada local vigente de la organizacion (US39).</summary>
    public async Task<SiteComparison> BySiteAsync(Guid organizationId, int days,
        CancellationToken ct = default)
    {
        var desde = Desde(days);
        var locales = new List<SiteShare>();
        double total = 0;
        string? moneda = null;

        foreach (var local in await _sites.ActiveSitesOfAsync(organizationId, ct))
        {
            var medidores = await _meters.FindBySiteIdAsync(local.SiteId, ct);
            var ids = medidores.Select(m => m.Id.ToString()).ToList();

            var consumo = new Acumulado();
            foreach (var lectura in await _readings.FindByMetersAsync(ids, desde, ct))
            {
                consumo.Suma(lectura, local.ExcludesSundaysFromPeak);
            }

            var tarifa = TarifaDe(local.TariffCategory);
            moneda ??= Moneda(tarifa);
            total += consumo.Kwh;

            locales.Add(new SiteShare(local.SiteId, local.SiteCode, local.Name,
                local.ContractedPowerKw, local.TariffCategory, ids.Count, Redondea(consumo.Kwh),
                0, Coste(consumo, tarifa), PorKwContratado(consumo.Kwh, local.ContractedPowerKw),
                Redondea(consumo.PicoKw)));
        }

        var conPorcentaje = locales
            .Select(l => l with { SharePercent = Porcentaje(l.Kwh, total) })
            .OrderByDescending(l => l.Kwh)
            .ToList();

        return new SiteComparison(organizationId, Dias(days), desde, DateTime.UtcNow,
            Redondea(total), moneda ?? "PEN", conPorcentaje);
    }

    /// <summary>Potencia que el local esta demandando ahora mismo (US24).</summary>
    /// <remarks>
    /// Se suma la ultima lectura de cada medidor del local. No es la demanda
    /// facturable —esa es la maxima del mes en intervalos de quince minutos—
    /// pero es lo que el supervisor necesita para saber como esta operando.
    /// </remarks>
    public async Task<CurrentConsumption> CurrentAsync(Guid siteId, CancellationToken ct = default)
    {
        var local = await _sites.SiteSummaryAsync(siteId, ct)
                    ?? throw AppException.NotFound($"site '{siteId}' not found");

        var medidores = await _meters.FindBySiteIdAsync(siteId, ct);
        double potenciaKw = 0;
        DateTime? medidoEn = null;

        foreach (var medidor in medidores)
        {
            var ultima = await _readings.FindLatestByMeterAsync(medidor.Id.ToString(), ct);
            if (ultima is null)
            {
                continue;
            }
            potenciaKw += ultima.PowerWatts / 1000d;
            if (medidoEn is null || ultima.Timestamp > medidoEn)
            {
                medidoEn = ultima.Timestamp;
            }
        }

        double? ocupacion = local.ContractedPowerKw > 0
            ? Redondea(potenciaKw / (double)local.ContractedPowerKw * 100)
            : null;

        return new CurrentConsumption(siteId, local.Name, medidores.Count, Redondea(potenciaKw),
            local.ContractedPowerKw, ocupacion, medidoEn);
    }

    // ------------------------------------------------------------------ apoyo

    private int Dias(int days) => days <= 0 ? DiasPorDefecto : Math.Min(days, DiasMaximo);

    private DateTime Desde(int days) => DateTime.UtcNow.AddDays(-Dias(days));

    private CommercialTariff? TarifaDe(string? categoria)
    {
        if (string.IsNullOrWhiteSpace(categoria))
        {
            return null;
        }
        try
        {
            return _pricing.CurrentTariff(categoria);
        }
        catch (Exception)
        {
            // Un local sin tarifa reconocida sigue teniendo consumo que mostrar;
            // lo que no se puede es ponerle precio.
            return null;
        }
    }

    private string Moneda(CommercialTariff? tarifa) => tarifa?.Currency ?? "PEN";

    private static decimal? Coste(Acumulado consumo, CommercialTariff? tarifa) =>
        tarifa is null
            ? null
            : Math.Round((decimal)consumo.KwhPunta * tarifa.EnergiaPuntaPorKwh
                         + (decimal)consumo.KwhFueraDePunta * tarifa.EnergiaFueraDePuntaPorKwh, 2);

    private static double? PorKwContratado(double kwh, decimal potenciaContratada) =>
        potenciaContratada <= 0 ? null : Redondea(kwh / (double)potenciaContratada);

    private static double Porcentaje(double parte, double total) =>
        total <= 0 ? 0 : Math.Round(parte / total * 100, 1);

    private static double Redondea(double valor) => Math.Round(valor, 2);

    /// <summary>Acumulador de una zona o de un local mientras se recorren las lecturas.</summary>
    private sealed class Acumulado
    {
        public double KwhPunta { get; private set; }
        public double KwhFueraDePunta { get; private set; }
        public double PicoKw { get; private set; }

        public double Kwh => KwhPunta + KwhFueraDePunta;

        public void Suma(EnergyReading lectura, bool excluyeDomingos)
        {
            if (HorarioPunta.FranjaDe(lectura.Timestamp, excluyeDomingos) == FranjaHoraria.PUNTA)
            {
                KwhPunta += lectura.EnergyKwh;
            }
            else
            {
                KwhFueraDePunta += lectura.EnergyKwh;
            }
            PicoKw = Math.Max(PicoKw, lectura.PowerWatts / 1000d);
        }
    }
}
