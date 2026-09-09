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
        var basico = SubscriptionPlan.Create("Starter",
            "One site, with the essentials to start measuring", 0, "PEN", "monthly");
        basico.AddFeature("BASIC_DASHBOARD", "Site consumption dashboard", "enabled");
        basico.AddFeature("CONSUMPTION_ALERTS", "Consumption alerts", "enabled");
        basico.AddFeature("SITES_LIMIT", "Sites included", "1");
        basico.AddFeature("DEVICES_PER_SITE_LIMIT", "Meters per site", "10");
        AddStripePrice(basico, "Stripe:Price:Free", "STRIPE_PRICE_FREE");
        await _plans.SaveAsync(basico, ct);

        var negocio = SubscriptionPlan.Create("Business",
            "For small chains, with demand control", 149, "PEN", "monthly");
        negocio.AddFeature("BASIC_INCLUDED", "Everything in Starter", "enabled");
        negocio.AddFeature("SITES_LIMIT", "Sites included", "5");
        negocio.AddFeature("DEVICES_PER_SITE_LIMIT", "Meters per site", "50");
        negocio.AddFeature("ZONE_ANALYTICS", "Consumption broken down by zone", "enabled");
        // El aviso de demanda es lo que justifica el salto de plan: evitar un
        // solo pico al mes ya paga la diferencia con el plan Basico.
        negocio.AddFeature("DEMAND_ALERTS", "Demand warning before exceeding the contracted power", "enabled");
        negocio.AddFeature("PEAK_HOUR_REPORTS", "Peak-hour consumption reports", "enabled");
        AddStripePrice(negocio, "Stripe:Price:Plus", "STRIPE_PRICE_PLUS");
        await _plans.SaveAsync(negocio, ct);

        var corporativo = SubscriptionPlan.Create("Enterprise",
            "Large chains, with site-to-site benchmarking", 399, "PEN", "monthly");
        corporativo.AddFeature("BUSINESS_INCLUDED", "Everything in Business", "enabled");
        corporativo.AddFeature("SITES_LIMIT", "Sites included", "unlimited");
        corporativo.AddFeature("DEVICES_PER_SITE_LIMIT", "Meters per site", "unlimited");
        corporativo.AddFeature("SITE_BENCHMARKING", "Performance benchmarking across sites", "enabled");
        corporativo.AddFeature("TARIFF_OPTIMIZATION", "Tariff category analysis", "enabled");
        corporativo.AddFeature("PRIORITY_SUPPORT", "Priority support", "enabled");
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
