using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sems.Api.Shared.Events;

namespace Sems.Api.Shared.Persistence;

/// <summary>
/// Construye el <see cref="SemsDbContext"/> para las herramientas de linea de
/// comandos (<c>dotnet ef migrations add</c>, <c>dotnet ef database update</c>).
/// </summary>
/// <remarks>
/// Sin esta clase, <c>dotnet ef</c> intenta arrancar la aplicacion entera para
/// encontrar el contexto, y eso obliga a tener configuradas todas las variables
/// de entorno solo para generar una migracion.
///
/// La cadena que se usa aqui no conecta con nada: al generar el SQL de una
/// migracion, EF Core solo necesita saber que el proveedor es PostgreSQL. Si
/// existe DATABASE_URL se usa esa, para que <c>database update</c> tambien
/// funcione contra la base real.
/// </remarks>
public sealed class SemsDbContextFactory : IDesignTimeDbContextFactory<SemsDbContext>
{
    public SemsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
                               ?? "Host=localhost;Port=5432;Database=sems;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<SemsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        // Las migraciones no publican eventos de dominio: solo generan SQL a
        // partir del modelo. Un bus que no hace nada evita arrastrar aqui todo
        // el contenedor de dependencias de la aplicacion.
        return new SemsDbContext(options, new NullDomainEventBus());
    }

    private sealed class NullDomainEventBus : IDomainEventBus
    {
        public void Publish(IDomainEvent domainEvent)
        {
        }
    }
}
