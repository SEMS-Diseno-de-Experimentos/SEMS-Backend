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
        var basico = SubscriptionPlan.Create("Básico",
            "Un local, con lo esencial para empezar a medir", 29.90, "PEN", "monthly");
        basico.AddFeature("BASIC_DASHBOARD", "Dashboard de consumo por local", "enabled");
        basico.AddFeature("CONSUMPTION_ALERTS", "Alertas de consumo", "enabled");
        basico.AddFeature("SITES_LIMIT", "Locales incluidos", "1");
        basico.AddFeature("DEVICES_PER_SITE_LIMIT", "Medidores por local", "10");
        AddStripePrice(basico, "Stripe:Price:Basico", "STRIPE_PRICE_BASICO", "STRIPE_PRICE_FREE");
        await _plans.SaveAsync(basico, ct);

        var negocio = SubscriptionPlan.Create("Pro",
            "Para pequeñas cadenas, con control de demanda", 79.90, "PEN", "monthly");
        negocio.AddFeature("BASIC_INCLUDED", "Todo lo del plan Básico", "enabled");
        negocio.AddFeature("SITES_LIMIT", "Locales incluidos", "5");
        negocio.AddFeature("DEVICES_PER_SITE_LIMIT", "Medidores por local", "50");
        negocio.AddFeature("ZONE_ANALYTICS", "Consumo desglosado por zona", "enabled");
        negocio.AddFeature("DEMAND_ALERTS", "Aviso de demanda antes de superar potencia contratada", "enabled");
        negocio.AddFeature("PEAK_HOUR_REPORTS", "Reportes de consumo en hora punta", "enabled");
        AddStripePrice(negocio, "Stripe:Price:Pro", "STRIPE_PRICE_PRO_PLAN", "STRIPE_PRICE_PLUS");
        await _plans.SaveAsync(negocio, ct);

        var corporativo = SubscriptionPlan.Create("Enterprise",
            "Grandes cadenas, con benchmarking entre locales", 129.90, "PEN", "monthly");
        corporativo.AddFeature("PRO_INCLUDED", "Todo lo del plan Pro", "enabled");
        corporativo.AddFeature("SITES_LIMIT", "Locales incluidos", "ilimitado");
        corporativo.AddFeature("DEVICES_PER_SITE_LIMIT", "Medidores por local", "ilimitado");
        corporativo.AddFeature("SITE_BENCHMARKING", "Benchmarking de eficiencia entre locales", "enabled");
        corporativo.AddFeature("TARIFF_OPTIMIZATION", "Análisis de categoría tarifaria", "enabled");
        corporativo.AddFeature("PRIORITY_SUPPORT", "Soporte prioritario", "enabled");
        AddStripePrice(corporativo, "Stripe:Price:Enterprise", "STRIPE_PRICE_ENTERPRISE", "STRIPE_PRICE_PRO");
        await _plans.SaveAsync(corporativo, ct);
    }

    /// <summary>
    /// Ata el plan a su precio de Stripe, si esta configurado.
    /// </summary>
    /// <remarks>
    /// <para>Se acepta tambien el nombre antiguo de la variable para no romper
    /// un entorno que ya la tenga puesta. Los nombres viejos venian del catalogo
    /// anterior (Free, Plus, Pro) y ya no decian la verdad: el plan Pro leia
    /// <c>STRIPE_PRICE_PLUS</c> y Enterprise leia <c>STRIPE_PRICE_PRO</c>, de
    /// modo que configurar "el precio de Pro" cobraba el de Enterprise.</para>
    /// </remarks>
    private void AddStripePrice(SubscriptionPlan plan, string configKey, string envKey,
        string envKeyAntiguo)
    {
        var priceId = _configuration[configKey]
                      ?? Environment.GetEnvironmentVariable(envKey)
                      ?? Environment.GetEnvironmentVariable(envKeyAntiguo);
        if (!string.IsNullOrWhiteSpace(priceId))
        {
            plan.AddFeature(PlanFeature.StripePriceIdCode, "Stripe price id", priceId);
        }
    }
}
