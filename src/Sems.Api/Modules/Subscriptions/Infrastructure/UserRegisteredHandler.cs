using Sems.Api.Modules.Subscriptions.Application;
using Sems.Api.Modules.Subscriptions.Domain.Repositories;
using Sems.Api.Shared.Events;

namespace Sems.Api.Modules.Subscriptions.Infrastructure;

/// <summary>
/// Pone a toda cuenta nueva en el plan de entrada.
/// </summary>
/// <remarks>
/// <para>El plan de entrada es el mas barato del catalogo, y se elige por su
/// precio y no por su nombre: los nombres cambian —el catalogo y la landing no
/// coinciden hoy— y un plan renombrado no debe dejar a las cuentas nuevas sin
/// suscripcion.</para>
///
/// <para>El cupo de locales de ese plan es de uno, asi que es importante que la
/// cuenta no nazca con locales inventados: ese local seria el unico que le
/// caben y el cliente no podria dar de alta el suyo. Por eso los datos de
/// demostracion vienen apagados de serie.</para>
/// </remarks>
public sealed class SubscriptionUserRegisteredHandler : IDomainEventHandler<DomainEvents.UserRegistered>
{
    private readonly SubscriptionService _subscriptions;
    private readonly IPlanRepository _plans;
    private readonly ILogger<SubscriptionUserRegisteredHandler> _logger;

    public SubscriptionUserRegisteredHandler(SubscriptionService subscriptions, IPlanRepository plans, ILogger<SubscriptionUserRegisteredHandler> logger)
    {
        _subscriptions = subscriptions;
        _plans = plans;
        _logger = logger;
    }

    public async Task HandleAsync(DomainEvents.UserRegistered e, CancellationToken ct = default)
    {
        var entrada = (await _plans.FindAllActiveAsync(ct))
            .OrderBy(p => p.Price)
            .FirstOrDefault();

        if (entrada is null)
        {
            _logger.LogWarning(
                "El catalogo no tiene planes activos: la cuenta {UserId} queda sin suscripcion",
                e.UserId);
            return;
        }

        await _subscriptions.CreateAsync(e.UserId.ToString(), entrada.PlanId, null, ct);
        _logger.LogInformation("Asignado el plan de entrada {PlanId} a la cuenta {UserId}",
            entrada.PlanId, e.UserId);
    }
}
