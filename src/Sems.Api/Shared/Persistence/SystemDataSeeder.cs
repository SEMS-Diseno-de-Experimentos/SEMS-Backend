using System;

namespace Sems.Api.Shared.Persistence;

/// <summary>
/// Interruptor de los datos de demostracion.
/// </summary>
/// <remarks>
/// <para>Esta clase tuvo dos metodos de sembrado que se retiraron, cada uno por
/// su propia razon.</para>
///
/// <para><c>SeedAsync</c> se llamaba al arrancar, sin pasar por este
/// interruptor, y creaba la cuenta <c>admin@energix.com</c> con una contrasena
/// fija escrita en el codigo. El repositorio es publico y la verificacion de
/// correo esta apagada por defecto, de modo que cualquiera que leyera el
/// archivo podia iniciar sesion en cualquier despliegue con la base en pie. La
/// cuenta no se sustituye por otra: el registro normal ya crea cuentas.</para>
///
/// <para><c>SeedForUserAsync</c> insertaba locales, zonas y equipos con SQL
/// crudo contra <c>org_sites</c> y <c>org_zones</c>, tablas que no existen (son
/// <c>og_sites</c> y <c>og_zones</c>), con nombres de columna que tampoco se
/// corresponden con los que genera Entity Framework. Fallaba siempre y la
/// excepcion se descartaba en el controlador, asi que nunca sembro nada. El
/// sembrado que si funciona vive en <c>AuthenticationService</c> y pasa por el
/// mismo interruptor de abajo.</para>
/// </remarks>
public static class SystemDataSeeder
{
    /// <summary>
    /// Si se cargan datos de demostracion al registrar una cuenta.
    /// </summary>
    /// <remarks>
    /// <para>Apagado salvo que <c>Seeding:DemoData</c> (o <c>SEED_DEMO_DATA</c>)
    /// valga <c>true</c>. Antes venia encendido, y eso hacia dos danos a toda
    /// cuenta real: la primera pantalla mostraba una organizacion inventada con
    /// nueve aparatos y consumos aleatorios, y ese local ficticio agotaba el
    /// unico que incluye el plan de entrada, de modo que el cliente no podia dar
    /// de alta el suyo sin cambiar de plan.</para>
    ///
    /// <para>Sigue disponible para las demostraciones: basta
    /// <c>SEED_DEMO_DATA=true</c> en el entorno donde se vaya a ensenar.</para>
    /// </remarks>
    public static bool DemoDataEnabled(IConfiguration configuration) =>
        string.Equals(configuration["Seeding:DemoData"]
                      ?? Environment.GetEnvironmentVariable("SEED_DEMO_DATA"),
            "true", StringComparison.OrdinalIgnoreCase);
}
