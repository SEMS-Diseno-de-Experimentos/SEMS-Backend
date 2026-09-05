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

        var free = SubscriptionPlan.Create("Free", "Start monitoring at no cost", 0, "PEN", "monthly");
        free.AddFeature("BASIC_DASHBOARD", "Basic energy dashboard", "enabled");
        free.AddFeature("CONSUMPTION_ALERTS", "Essential consumption alerts", "enabled");
        free.AddFeature("LINKED_DEVICES_LIMIT", "Linked devices limit", "3");
        AddStripePrice(free, "Stripe:Price:Free", "STRIPE_PRICE_FREE");
        await _plans.SaveAsync(free, ct);

        var plus = SubscriptionPlan.Create("Plus", "For active homes", 15, "PEN", "monthly");
        plus.AddFeature("FREE_INCLUDED", "Everything in Free", "enabled");
        plus.AddFeature("DEVICE_ANALYTICS", "Detailed device analytics", "enabled");
        plus.AddFeature("SAVING_RECOMMENDATIONS", "Personalized saving recommendations", "enabled");
        plus.AddFeature("MONTHLY_REPORTS", "Monthly savings reports", "enabled");
        plus.AddFeature("LINKED_DEVICES_LIMIT", "Linked devices limit", "10");
        AddStripePrice(plus, "Stripe:Price:Plus", "STRIPE_PRICE_PLUS");
        await _plans.SaveAsync(plus, ct);

        var pro = SubscriptionPlan.Create("Pro", "Advanced control and insights", 25, "PEN", "monthly");
        pro.AddFeature("PLUS_INCLUDED", "Everything in Plus", "enabled");
        pro.AddFeature("UNLIMITED_DEVICES", "Unlimited linked devices", "enabled");
        pro.AddFeature("PRIORITY_SUPPORT", "Priority support", "enabled");
        AddStripePrice(pro, "Stripe:Price:Pro", "STRIPE_PRICE_PRO");
        await _plans.SaveAsync(pro, ct);
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
