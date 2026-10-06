using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Sems.Api.Modules.Alerts.Domain.Services;
using Sems.Api.Modules.Subscriptions.Infrastructure;
using Sems.Api.Shared.Persistence;

namespace Sems.Api.TestSupport;

/// <summary>
/// Levanta la API completa en memoria: la misma composicion de <c>Program.cs</c>,
/// el middleware de errores, la autenticacion JWT y los controladores reales.
/// </summary>
/// <remarks>
/// <para>Solo se reemplazan dos piezas: la base de datos, que pasa a ser SQLite
/// en memoria (una por instancia de la fabrica), y el envio de correo, que se
/// sustituye por <see cref="FakeEmailSender"/> para poder comprobar que salio.</para>
///
/// <para>Se usa SQLite y no el proveedor InMemory de EF Core porque este ultimo
/// no aplica indices unicos: sin ellos, las pruebas de RUC, codigo de local,
/// numero de serie o correo repetidos no probarian nada.</para>
/// </remarks>
public class SemsApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Entorno con el que arranca la API durante las pruebas.</summary>
    public const string TestingEnvironment = "Testing";

    /// <summary>Clave de firma JWT de prueba (32 caracteres o mas, como exige HS256).</summary>
    public const string JwtSecret = "sems-test-jwt-secret-key-0123456789-abcdefghij";

    /// <summary>Secreto de prueba con el que se firman los eventos de Stripe.</summary>
    public const string StripeWebhookSecret = "whsec_sems_test_0123456789abcdefghijklmnop";

    /// <summary>Contrasena que usan las cuentas creadas por las pruebas.</summary>
    public const string DefaultPassword = "SecurePass123";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    static SemsApiFactory()
    {
        // Variables y no claves de configuracion: Program.cs lee el secreto JWT
        // antes de construir la aplicacion, y la variable de entorno es lo unico
        // que con seguridad ya esta disponible en ese momento.
        Environment.SetEnvironmentVariable("JWT_SECRET", JwtSecret);
        Environment.SetEnvironmentVariable("STRIPE_WEBHOOK_SECRET", StripeWebhookSecret);

        // Un espacio y no null: DotEnv.Load rellena las variables ausentes desde
        // un .env, y con eso las pruebas acabarian conectadas a la base real.
        Environment.SetEnvironmentVariable("DATABASE_URL", " ");

        // La siembra de datos de demostracion corre en segundo plano tras cada
        // registro y competiria con la prueba por la base de datos.
        Environment.SetEnvironmentVariable("SEED_DEMO_DATA", "false");

        const string contentRootVariable = "ASPNETCORE_TEST_CONTENTROOT_SEMS_API";
        if (Environment.GetEnvironmentVariable(contentRootVariable) is null
            && FindApiContentRoot() is { } contentRoot)
        {
            Environment.SetEnvironmentVariable(contentRootVariable, contentRoot);
        }
    }

    /// <summary>Correos que la aplicacion intento enviar.</summary>
    public FakeEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(TestingEnvironment);
        // Cadena vacia: Program.cs no intenta migrar ni sembrar contra Postgres.
        builder.UseSetting("ConnectionStrings:Default", string.Empty);
        builder.UseSetting("Seeding:DemoData", "false");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<SemsDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<SemsDbContext>(ConfigureDatabase);

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }

    /// <summary>Base de datos de la instancia: SQLite en memoria, abierta mientras viva la fabrica.</summary>
    protected virtual void ConfigureDatabase(DbContextOptionsBuilder options)
    {
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            _connection.Open();
        }
        options.UseSqlite(_connection);
    }

    /// <summary>Si se crea el esquema y se cargan los planes al arrancar.</summary>
    protected virtual bool PrepareDatabase => true;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        if (PrepareDatabase)
        {
            using var scope = host.Services.CreateScope();
            // EnsureCreated y no migraciones: las migraciones estan escritas
            // para PostgreSQL. El esquema sale del mismo modelo.
            scope.ServiceProvider.GetRequiredService<SemsDbContext>().Database.EnsureCreated();
            // Los planes se cargan con el mismo seeder que en produccion.
            scope.ServiceProvider.GetRequiredService<PlanSeeder>().SeedAsync()
                .GetAwaiter().GetResult();
        }

        return host;
    }

    /// <summary>Ejecuta una consulta directa contra la base de la instancia.</summary>
    public async Task<T> QueryDatabaseAsync<T>(Func<SemsDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<SemsDbContext>());
    }

    /// <summary>
    /// Crea una cuenta como lo haria un cliente: registro e inicio de sesion
    /// contra <c>/api/v1/auth</c>. Devuelve un cliente HTTP ya autenticado.
    /// </summary>
    public async Task<TestUser> CreateUserAsync(string? email = null,
        string password = DefaultPassword)
    {
        email ??= $"user-{Guid.NewGuid():N}@sems.test";
        var anonymous = CreateClient();

        var registered = await anonymous.PostAsJsonAsync("/api/v1/auth/register",
            new { emailAddress = email, password });
        await registered.ExpectAsync(System.Net.HttpStatusCode.OK);

        var login = await anonymous.PostAsJsonAsync("/api/v1/auth/login",
            new { emailAddress = email, password });
        var session = await login.ExpectAsync(System.Net.HttpStatusCode.OK);

        var token = session.GetProperty("token").GetString()!;
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return new TestUser(
            session.GetProperty("userId").GetGuid(),
            email.Trim().ToLowerInvariant(),
            password,
            token,
            session.GetProperty("refreshToken").GetString()!,
            client);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }

    /// <summary>Carpeta de la API, buscando la solucion hacia arriba.</summary>
    private static string? FindApiContentRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SemsBackend.sln")))
            {
                return Path.Combine(dir.FullName, "src", "Sems.Api");
            }
        }
        return null;
    }
}

/// <summary>
/// Variante de la fabrica cuya base de datos no se puede abrir. Sirve para
/// comprobar como responde la API cuando la base esta caida.
/// </summary>
public sealed class UnavailableDatabaseSemsApiFactory : SemsApiFactory
{
    private readonly string _missingFile = Path.Combine(Path.GetTempPath(),
        $"sems-missing-{Guid.NewGuid():N}", "sems.db");

    protected override bool PrepareDatabase => false;

    protected override void ConfigureDatabase(DbContextOptionsBuilder options) =>
        // Solo lectura sobre un archivo que no existe: SQLite no puede abrirlo.
        options.UseSqlite($"Data Source={_missingFile};Mode=ReadOnly");
}

/// <summary>Cuenta creada por la prueba, con su cliente ya autenticado.</summary>
public sealed record TestUser(Guid UserId, string Email, string Password, string Token,
    string RefreshToken, HttpClient Client);
