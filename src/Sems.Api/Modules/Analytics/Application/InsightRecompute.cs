using System.Collections.Concurrent;

namespace Sems.Api.Modules.Analytics.Application;

/// <summary>
/// Cola de usuarios pendientes de recalcular sus hallazgos.
/// </summary>
/// <remarks>
/// <para>Es un <i>debounce</i>, no un acumulador: cada aviso reinicia la espera
/// del mismo usuario. Un medidor que reporta cada minuto pediria un recalculo
/// por minuto, y una importacion masiva pediria uno por lectura.</para>
///
/// <para>La espera tambien evita un error mas sutil, que es el que hizo falta
/// corregir en el backend hermano: al calcular con la primera lectura de una
/// importacion, los hallazgos salian de un solo dia, y la idempotencia diaria
/// impedia despues corregirlos. Esperar a que dejen de llegar lecturas hace que
/// el calculo vea el lote completo.</para>
/// </remarks>
public sealed class InsightRecomputeQueue
{
    /// <summary>Lo que se espera desde el ultimo aviso antes de recalcular.</summary>
    public static readonly TimeSpan Espera = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<string, DateTime> _pendientes = new();

    /// <summary>Anota que este usuario tiene consumo nuevo.</summary>
    public void Pedir(string userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            _pendientes[userId] = DateTime.UtcNow;
        }
    }

    /// <summary>Usuarios cuya espera ya vencio; salen de la cola al devolverlos.</summary>
    public List<string> Vencidos(DateTime ahora)
    {
        if (_pendientes.IsEmpty)
        {
            return new List<string>();
        }

        var vencidos = new List<string>();
        foreach (var par in _pendientes)
        {
            if (ahora - par.Value < Espera)
            {
                continue;
            }
            // Si entre la lectura del par y aqui llego otro aviso, el valor ya
            // no coincide y el usuario se queda para la siguiente vuelta.
            if (_pendientes.TryRemove(new KeyValuePair<string, DateTime>(par.Key, par.Value)))
            {
                vencidos.Add(par.Key);
            }
        }
        return vencidos;
    }

    public bool HayPendientes => !_pendientes.IsEmpty;
}

/// <summary>
/// Vacia la cola de recalculos cuando las lecturas dejan de llegar.
/// </summary>
/// <remarks>
/// Abre un ambito nuevo por usuario: el motor de hallazgos y sus repositorios
/// son de ambito de peticion, y aqui no hay peticion. Un fallo de un usuario no
/// debe dejar sin calcular a los demas, asi que se registra y se sigue.
/// </remarks>
public sealed class InsightRecomputeWorker : BackgroundService
{
    private static readonly TimeSpan Latido = TimeSpan.FromSeconds(1);

    private readonly InsightRecomputeQueue _cola;
    private readonly IServiceScopeFactory _ambitos;
    private readonly ILogger<InsightRecomputeWorker> _logger;

    public InsightRecomputeWorker(InsightRecomputeQueue cola, IServiceScopeFactory ambitos,
        ILogger<InsightRecomputeWorker> logger)
    {
        _cola = cola;
        _ambitos = ambitos;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var reloj = new PeriodicTimer(Latido);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await reloj.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!_cola.HayPendientes)
            {
                continue;
            }

            foreach (var userId in _cola.Vencidos(DateTime.UtcNow))
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                await RecalcularAsync(userId, stoppingToken);
            }
        }
    }

    private async Task RecalcularAsync(string userId, CancellationToken ct)
    {
        try
        {
            using var ambito = _ambitos.CreateScope();
            var motor = ambito.ServiceProvider.GetRequiredService<InsightEngine>();
            await motor.RecalcularAsync(userId, ct);
        }
        catch (OperationCanceledException)
        {
            // El servidor se esta apagando; no es un fallo.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudieron recalcular los hallazgos de {UserId}", userId);
        }
    }
}
