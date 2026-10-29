using Sems.Api.Shared.Events;

namespace Sems.Api.Modules.Analytics.Application;

/// <summary>
/// Pide recalcular los hallazgos cuando llega consumo nuevo.
/// </summary>
/// <remarks>
/// No calcula aqui mismo a proposito: el calculo recorre un mes de lecturas, y
/// hacerlo dentro de la peticion que registra una lectura la volveria lenta por
/// un trabajo que al cliente no le urge. Se encola, y la cola espera a que las
/// lecturas dejen de llegar.
/// </remarks>
public sealed class InsightEventHandler : IDomainEventHandler<DomainEvents.ReadingProcessed>
{
    private readonly InsightRecomputeQueue _cola;

    public InsightEventHandler(InsightRecomputeQueue cola) => _cola = cola;

    public Task HandleAsync(DomainEvents.ReadingProcessed e, CancellationToken ct = default)
    {
        _cola.Pedir(e.UserId.ToString());
        return Task.CompletedTask;
    }
}
