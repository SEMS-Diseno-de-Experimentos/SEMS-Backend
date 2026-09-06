using Sems.Api.Modules.Subscriptions.Domain.Model;
using Sems.Api.Modules.Subscriptions.Domain.Repositories;

namespace Sems.Api.Modules.Subscriptions.Infrastructure;

/// <summary>
/// Carga los tres planes por defecto la primera vez que arranca el sistema.
///
/// <para>Solo actua si no hay ningun plan, de modo que reiniciar la aplicacion
/// nunca duplica ni sobreescribe lo existente.</para>
///
/// <para>El limite de dispositivos vive como caracteristica del plan
/// (<c>LINKED_DEVICES_LIMIT</c>) y no en el codigo: asi se puede cambiar sin
/// volver a desplegar.</para>
/// </summary>
public sealed class PlanSeeder
{
    private readonly IPlanRepository _plans;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PlanSeeder> _logger;

    public PlanSeeder(IPlanRepository plans, IConfiguration configuration, ILogger<PlanSeeder> logger)
    {
        _plans = plans;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await _plans.CountAsync(ct) > 0)
        {
            return;
        }
        _logger.LogInformation("No hay planes registrados; cargando los tres por defecto");

        // Los planes se miden en LOCALES, no en dispositivos.
        //
        // En el segmento anterior el limite de dispositivos tenia sentido: una
        // vivienda tiene unos pocos. Un supermercado tiene decenas de medidores
        // en un solo local, asi que un tope de tres o diez no separa a un
        // cliente pequeno de una cadena; solo estorba. Lo que de verdad escala
        // con el tamano del cliente es cuantos locales gestiona.
        var basico = SubscriptionPlan.Create("Basico",
            "Un local, con lo necesario para empezar a medir", 0, "PEN", "monthly");
        basico.AddFeature("BASIC_DASHBOARD", "Panel de consumo del local", "enabled");
        basico.AddFeature("CONSUMPTION_ALERTS", "Alertas de consumo", "enabled");
        basico.AddFeature("SITES_LIMIT", "Locales incluidos", "1");
        basico.AddFeature("DEVICES_PER_SITE_LIMIT", "Medidores por local", "10");
        AddStripePrice(basico, "Stripe:Price:Free", "STRIPE_PRICE_FREE");
        await _plans.SaveAsync(basico, ct);

        var negocio = SubscriptionPlan.Create("Negocio",
            "Para cadenas pequenas, con control de demanda", 149, "PEN", "monthly");
        negocio.AddFeature("BASIC_INCLUDED", "Todo lo del plan Basico", "enabled");
        negocio.AddFeature("SITES_LIMIT", "Locales incluidos", "5");
        negocio.AddFeature("DEVICES_PER_SITE_LIMIT", "Medidores por local", "50");
        negocio.AddFeature("ZONE_ANALYTICS", "Consumo desglosado por zona", "enabled");
        // El aviso de demanda es lo que justifica el salto de plan: evitar un
        // solo pico al mes ya paga la diferencia con el plan Basico.
        negocio.AddFeature("DEMAND_ALERTS", "Aviso de demanda antes de superar lo contratado", "enabled");
        negocio.AddFeature("PEAK_HOUR_REPORTS", "Reportes de consumo en hora punta", "enabled");
        AddStripePrice(negocio, "Stripe:Price:Plus", "STRIPE_PRICE_PLUS");
        await _plans.SaveAsync(negocio, ct);

        var corporativo = SubscriptionPlan.Create("Corporativo",
            "Cadenas grandes, con comparacion entre locales", 399, "PEN", "monthly");
        corporativo.AddFeature("BUSINESS_INCLUDED", "Todo lo del plan Negocio", "enabled");
        corporativo.AddFeature("SITES_LIMIT", "Locales incluidos", "unlimited");
        corporativo.AddFeature("DEVICES_PER_SITE_LIMIT", "Medidores por local", "unlimited");
        corporativo.AddFeature("SITE_BENCHMARKING", "Comparacion de rendimiento entre locales", "enabled");
        corporativo.AddFeature("TARIFF_OPTIMIZATION", "Analisis de categoria tarifaria", "enabled");
        corporativo.AddFeature("PRIORITY_SUPPORT", "Soporte prioritario", "enabled");
        AddStripePrice(corporativo, "Stripe:Price:Pro", "STRIPE_PRICE_PRO");
        await _plans.SaveAsync(corporativo, ct);
    }

    private void AddStripePrice(SubscriptionPlan plan, string configKey, string envKey)
    {
        var priceId = _configuration[configKey] ?? Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrWhiteSpace(priceId))
        {
            plan.AddFeature(PlanFeature.StripePriceIdCode, "Stripe price id", priceId);
        }
    }
}
