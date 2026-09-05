using Microsoft.Extensions.DependencyInjection;

namespace Sems.Api.Shared.Events;

/// <summary>Un consumidor de eventos de dominio.</summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}

/// <summary>Unico punto de salida de eventos de dominio del monolito.</summary>
public interface IDomainEventBus
{
    /// <summary>
    /// Encola un evento. <b>No se entrega de inmediato:</b> los consumidores lo
    /// reciben cuando la transaccion que lo origino confirma.
    /// </summary>
    void Publish(IDomainEvent domainEvent);
}

/// <summary>
/// Bus de eventos en proceso.
///
/// <para>Reemplaza al productor de Kafka. Los eventos se acumulan durante la
/// unidad de trabajo y se despachan solo cuando <c>SaveChangesAsync</c>
/// termina bien. Esa demora deliberada da la misma garantia que buscabamos con
/// el broker: <b>nadie reacciona a un cambio que despues se revierte</b>.</para>
///
/// <para>Es el equivalente de <c>@TransactionalEventListener(AFTER_COMMIT)</c>
/// en Spring. Si el proyecto vuelve a necesitar un broker, este es el unico
/// archivo que cambia.</para>
/// </summary>
public sealed class DomainEventBus : IDomainEventBus
{
    private readonly List<IDomainEvent> _pending = new();
    private readonly IServiceProvider _services;
    private readonly ILogger<DomainEventBus> _logger;

    public DomainEventBus(IServiceProvider services, ILogger<DomainEventBus> logger)
    {
        _services = services;
        _logger = logger;
    }

    public void Publish(IDomainEvent domainEvent)
    {
        _logger.LogDebug("Encolando {Event} para el usuario {UserId}",
            domainEvent.GetType().Name, domainEvent.UserId);
        _pending.Add(domainEvent);
    }

    /// <summary>
    /// Entrega los eventos acumulados. Lo invoca el DbContext tras confirmar.
    /// </summary>
    public async Task DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        if (_pending.Count == 0)
        {
            return;
        }

        // Se vacia la lista antes de despachar: un consumidor que a su vez
        // publique un evento no debe reentrar sobre la coleccion que se recorre.
        var toDispatch = _pending.ToArray();
        _pending.Clear();

        foreach (var domainEvent in toDispatch)
        {
            await DispatchOneAsync(domainEvent, cancellationToken);
        }
    }

    private async Task DispatchOneAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
        var handlers = _services.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            if (handler is null)
            {
                continue;
            }
            try
            {
                var method = handlerType.GetMethod("HandleAsync")!;
                await (Task)method.Invoke(handler, new object[] { domainEvent, cancellationToken })!;
            }
            catch (Exception ex)
            {
                // Un consumidor que falla no debe tumbar la operacion de negocio
                // que ya se confirmo: el cobro ocurrio aunque el correo no salga.
                _logger.LogError(ex, "El consumidor {Handler} fallo procesando {Event}",
                    handler.GetType().Name, domainEvent.GetType().Name);
            }
        }
    }
}
