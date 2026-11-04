namespace Sems.Api.Modules.Analytics.Domain.Services;

/// <summary>
/// Lo que analitica necesita saber del consumo medido, declarado por ella.
/// </summary>
/// <remarks>
/// <para>Es el mismo patron que <c>ISiteDirectory</c> en dispositivos: el puerto
/// lo declara quien consume, no quien sirve. Analitica no tiene por que conocer
/// el medidor, la lectura ni el pliego tarifario; solo cuanto se consumio cada
/// dia, donde, y a que precio se factura.</para>
///
/// <para>El dia viene ya resuelto en hora de Peru. Hacerlo aqui y no en el motor
/// de hallazgos es deliberado: la conversion depende de como se guardan las
/// lecturas, que es justo lo que este puerto oculta.</para>
/// </remarks>
public interface IEnergyUsage
{
    /// <summary>Consumo de un medidor en un dia natural.</summary>
    public sealed record DailyUsage(Guid MeterId, Guid? SiteId, Guid? ZoneId, DateOnly Day,
        double Kwh, double PeakKwh, double MaxPowerKw);

    /// <summary>Precios y datos de facturacion de un local.</summary>
    public sealed record SitePrices(string SiteName, string? TariffCategory,
        decimal ContractedPowerKw, string Currency, decimal PeakPerKwh, decimal OffPeakPerKwh,
        decimal PowerPerKwMonth);

    /// <summary>Zona vigente de un local.</summary>
    public sealed record ZoneName(Guid ZoneId, string Name);

    /// <summary>Consumo diario por medidor de los ultimos <paramref name="days"/> dias.</summary>
    Task<List<DailyUsage>> DailyUsageAsync(string userId, int days, CancellationToken ct = default);

    /// <summary>Precios del local, o null si el local no existe o no esta vigente.</summary>
    Task<SitePrices?> PricesOfAsync(Guid siteId, CancellationToken ct = default);

    /// <summary>Zonas vigentes del local.</summary>
    Task<List<ZoneName>> ZonesOfAsync(Guid siteId, CancellationToken ct = default);
}
