using Microsoft.EntityFrameworkCore;
using Sems.Api.Shared.Events;

namespace Sems.Api.Shared.Persistence;

/// <summary>
/// Contexto de datos del monolito.
///
/// <para>Una sola base de datos, pero cada modulo aisla sus tablas por prefijo
/// de nombre (<c>dm_</c> dispositivos, <c>em_</c> energia, <c>an_</c> analitica,
/// <c>al_</c> alertas, <c>pm_</c> pagos, <c>sb_</c> suscripciones, <c>iam_</c>
/// identidad). Separarlas en esquemas distintos mas adelante no exigiria tocar
/// el codigo de dominio.</para>
///
/// <para>La sobrecarga de <see cref="SaveChangesAsync"/> es la pieza clave del
/// disenio de eventos: los que se publicaron durante la operacion se entregan
/// <b>despues</b> de que la escritura confirme. Sin eso, un consumidor podria
/// reaccionar a un cambio que luego se revierte y, por ejemplo, enviar un correo
/// de bienvenida a un usuario que no llego a existir.</para>
/// </summary>
public class SemsDbContext : DbContext
{
    private readonly IDomainEventBus _eventBus;

    public SemsDbContext(DbContextOptions<SemsDbContext> options, IDomainEventBus eventBus)
        : base(options)
    {
        _eventBus = eventBus;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Cada modulo aporta su configuracion desde su propia capa de
        // infraestructura, de modo que este archivo no crece con el proyecto.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SemsDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var affected = await base.SaveChangesAsync(cancellationToken);

        // Solo se llega aqui si la escritura fue bien.
        if (_eventBus is DomainEventBus bus)
        {
            await bus.DispatchPendingAsync(cancellationToken);
        }

        return affected;
    }
}
