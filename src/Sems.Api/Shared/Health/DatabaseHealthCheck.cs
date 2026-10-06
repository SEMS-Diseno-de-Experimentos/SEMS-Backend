using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sems.Api.Shared.Persistence;

namespace Sems.Api.Shared.Health;

/// <summary>
/// Comprueba que la base de datos responde.
/// </summary>
/// <remarks>
/// Va etiquetada como "ready" y se expone solo en /health/ready, no en /health.
///
/// La separacion importa: el proveedor de hosting usa /health para decidir si el
/// proceso sigue vivo, y reinicia el contenedor cuando falla. Si ahi se mirara
/// la base de datos, un corte de Supabase provocaria reinicios en cadena que no
/// arreglan nada, porque el problema no esta en este proceso.
/// </remarks>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly SemsDbContext _db;

    public DatabaseHealthCheck(SemsDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("La base de datos responde")
                : HealthCheckResult.Unhealthy("La base de datos no acepta conexiones");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Error al conectar con la base de datos", ex);
        }
    }
}
