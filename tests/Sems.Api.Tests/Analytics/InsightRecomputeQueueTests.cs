using Sems.Api.Modules.Analytics.Application;
using Xunit;

namespace Sems.Api.Tests.Analytics;

/// <summary>
/// La espera que agrupa las peticiones de recalculo.
/// </summary>
/// <remarks>
/// Sin ella, una importacion de un mes de lecturas pediria un recalculo por
/// lectura, y el primero —hecho con un solo dia cargado— bloquearia a los
/// demas por la idempotencia diaria del motor. Esperar a que las lecturas dejen
/// de llegar es lo que hace que el calculo vea el lote entero.
/// </remarks>
public class InsightRecomputeQueueTests
{
    private readonly InsightRecomputeQueue _cola = new();

    private static readonly DateTime Ahora = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Vencidos_NothingRequested_ReturnsNobody()
    {
        Assert.Empty(_cola.Vencidos(Ahora));
        Assert.False(_cola.HayPendientes);
    }

    [Fact]
    public void Vencidos_BeforeTheWaitIsOver_ReturnsNobody()
    {
        _cola.Pedir("user-1");

        Assert.Empty(_cola.Vencidos(DateTime.UtcNow));
        Assert.True(_cola.HayPendientes);
    }

    [Fact]
    public void Vencidos_OnceTheWaitIsOver_ReturnsTheUserAndEmptiesTheQueue()
    {
        _cola.Pedir("user-1");

        Assert.Equal(new[] { "user-1" },
            _cola.Vencidos(DateTime.UtcNow + InsightRecomputeQueue.Espera));
        Assert.False(_cola.HayPendientes);
    }

    [Fact]
    public void Pedir_SeveralTimesForTheSameUser_RecomputesOnce()
    {
        // Es lo que ocurre con un medidor que reporta cada minuto: muchos
        // avisos, un solo recalculo.
        _cola.Pedir("user-1");
        _cola.Pedir("user-1");
        _cola.Pedir("user-1");

        Assert.Single(_cola.Vencidos(DateTime.UtcNow + InsightRecomputeQueue.Espera));
    }

    [Fact]
    public void Pedir_DifferentUsers_EachOneIsRecomputed()
    {
        _cola.Pedir("user-1");
        _cola.Pedir("user-2");

        Assert.Equal(new[] { "user-1", "user-2" },
            _cola.Vencidos(DateTime.UtcNow + InsightRecomputeQueue.Espera).OrderBy(u => u));
    }

    [Fact]
    public void Pedir_BlankUser_IsIgnored()
    {
        _cola.Pedir("   ");
        _cola.Pedir(string.Empty);

        Assert.False(_cola.HayPendientes);
    }
}
