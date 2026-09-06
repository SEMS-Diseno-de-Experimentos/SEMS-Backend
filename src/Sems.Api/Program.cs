using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Sems.Api.Modules.Devices.Application;
using Sems.Api.Modules.Devices.Domain.Repositories;
using Sems.Api.Modules.Devices.Domain.Services;
using Sems.Api.Modules.Devices.Infrastructure;
using Sems.Api.Modules.Energy.Application;
using Sems.Api.Modules.Energy.Domain.Repositories;
using Sems.Api.Modules.Energy.Domain.Services;
using Sems.Api.Modules.Energy.Infrastructure;
using Sems.Api.Modules.Analytics.Application;
using Sems.Api.Modules.Analytics.Domain.Repositories;
using Sems.Api.Modules.Analytics.Domain.Services;
using Sems.Api.Modules.Analytics.Infrastructure;
using Sems.Api.Modules.Organizations.Application;
using Sems.Api.Modules.Organizations.Domain.Repositories;
using Sems.Api.Modules.Organizations.Infrastructure;
using Sems.Api.Modules.Subscriptions.Application;
using Sems.Api.Modules.Subscriptions.Domain.Repositories;
using Sems.Api.Modules.Subscriptions.Domain.Services;
using Sems.Api.Modules.Subscriptions.Infrastructure;
using Sems.Api.Modules.Payments.Application;
using Sems.Api.Modules.Payments.Domain.Repositories;
using Sems.Api.Modules.Payments.Domain.Services;
using Sems.Api.Modules.Payments.Infrastructure;
using Sems.Api.Modules.Alerts.Application;
using Sems.Api.Modules.Alerts.Domain.Repositories;
using Sems.Api.Modules.Alerts.Domain.Services;
using Sems.Api.Modules.Alerts.Infrastructure;
using Sems.Api.Modules.Iam.Application;
using Sems.Api.Modules.Iam.Domain.Repositories;
using Sems.Api.Modules.Iam.Domain.Services;
using Sems.Api.Modules.Iam.Infrastructure;
using Sems.Api.Modules.Iam.Interfaces.Acl;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Sems.Api.Shared.Configuration;
using Sems.Api.Shared.Errors;
using Sems.Api.Shared.Events;
using Sems.Api.Shared.Http;
using Sems.Api.Shared.Persistence;

// Tiene que ir antes de construir la aplicacion: la configuracion se arma en
// CreateBuilder y ya no vuelve a mirar el entorno.
DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------- puerto
// Render (y casi cualquier proveedor) inyecta el puerto en PORT y espera que el
// proceso escuche exactamente ahi. El ASPNETCORE_URLS del Dockerfile fija 8080,
// que va bien en local pero deja al proveedor sin encontrar el servicio.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://+:{port}");
}

// ---------------------------------------------------------------------- JSON
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Los modulos que vienen de FastAPI usan snake_case y lo declaran con
        // [JsonPropertyName] en cada recurso. El resto va en camelCase.
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// --------------------------------------------------------------- persistencia
var connectionString = builder.Configuration.GetConnectionString("Default")
                       ?? Environment.GetEnvironmentVariable("DATABASE_URL")
                       ?? string.Empty;

builder.Services.AddDbContext<SemsDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseNpgsql(connectionString);
    }
});

// -------------------------------------------------------- eventos de dominio
// Scoped: los eventos se acumulan por peticion y se despachan cuando el
// DbContext de esa misma peticion confirma la escritura.
builder.Services.AddScoped<DomainEventBus>();
builder.Services.AddScoped<IDomainEventBus>(sp => sp.GetRequiredService<DomainEventBus>());

// ------------------------------------------------------------------- modulos
builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
builder.Services.AddScoped<IDeviceBindingRepository, DeviceBindingRepository>();
builder.Services.AddScoped<IDeviceConfigurationRepository, DeviceConfigurationRepository>();
builder.Services.AddScoped<IDeviceEventRepository, DeviceEventRepository>();
// El puerto que dispositivos usa para saber si un local y una zona existen.
builder.Services.AddScoped<ISiteDirectory, OrganizationsSiteDirectory>();
builder.Services.AddScoped<DeviceCommandService>();
builder.Services.AddScoped<DeviceQueryService>();

builder.Services.AddScoped<IEnergyMeterRepository, EnergyMeterRepository>();
builder.Services.AddScoped<IEnergyReadingRepository, EnergyReadingRepository>();
builder.Services.AddScoped<IDeviceConsumptionRepository, DeviceConsumptionRepository>();
builder.Services.AddScoped<IConsumptionAlertRepository, ConsumptionAlertRepository>();
builder.Services.AddSingleton<IEnergyPricingProvider, MockPlusEnergiaAdapter>();
builder.Services.AddScoped<EnergyCommandService>();
builder.Services.AddScoped<EnergyQueryService>();

builder.Services.AddScoped<IBillPredictionRepository, BillPredictionRepository>();
builder.Services.AddScoped<IRecommendationRepository, RecommendationRepository>();
builder.Services.AddScoped<IAnomalyRepository, AnomalyRepository>();
builder.Services.AddScoped<IDeviceIdentificationRepository, DeviceIdentificationRepository>();
builder.Services.AddScoped<IConsumptionRankingRepository, ConsumptionRankingRepository>();
builder.Services.AddScoped<IBillCalculator, EnergyBillCalculator>();
builder.Services.AddScoped<AnalyticsService>();

builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddSingleton<SubscriptionManager>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<PlanSeeder>();

// Organizaciones: la empresa, sus locales, las zonas de cada local y quien
// tiene acceso a que. Es el nivel que sustituye a la vivienda del segmento
// anterior.
builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddScoped<ISiteRepository, SiteRepository>();
builder.Services.AddScoped<IZoneRepository, ZoneRepository>();
builder.Services.AddScoped<IMembershipRepository, MembershipRepository>();
builder.Services.AddScoped<OrganizationCommandService>();
builder.Services.AddScoped<OrganizationQueryService>();

builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<IWebhookEventRepository, WebhookEventRepository>();
builder.Services.AddSingleton<IPaymentProvider, StripePaymentAdapter>();
builder.Services.AddSingleton<PaymentStatusMapper>();
builder.Services.AddScoped<PaymentCommandService>();
builder.Services.AddScoped<PaymentMethodCommandService>();
builder.Services.AddScoped<PaymentQueryService>();
builder.Services.AddScoped<WebhookCommandService>();

builder.Services.AddScoped<IAlertRepository, AlertRepository>();
builder.Services.AddScoped<IThresholdRepository, ThresholdRepository>();
builder.Services.AddScoped<IInactivityRuleRepository, InactivityRuleRepository>();
builder.Services.AddScoped<IDemandRuleRepository, DemandRuleRepository>();
builder.Services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();
builder.Services.AddScoped<INotificationLogRepository, NotificationLogRepository>();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<AlertCommandService>();
builder.Services.AddScoped<AlertQueryService>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IUserAuthTokenRepository, UserAuthTokenRepository>();
builder.Services.AddSingleton<IPasswordHashingService, BCryptPasswordHashingService>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IIamEventPublisher, InProcessIamEventPublisher>();
builder.Services.AddScoped<IIamAcl, IamAcl>();
builder.Services.AddScoped<AuthTokenService>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<AccountRecoveryService>();

// ------------------------------------------------- consumidores de eventos
// Cada uno reemplaza a un consumidor de un topic de Kafka.
builder.Services.AddScoped<IDomainEventHandler<DomainEvents.UserRegistered>, UserRegisteredHandler>();
builder.Services.AddScoped<IDomainEventHandler<DomainEvents.VerificationRequested>, VerificationRequestedHandler>();
builder.Services.AddScoped<IDomainEventHandler<DomainEvents.PasswordResetRequested>, PasswordResetRequestedHandler>();
builder.Services.AddScoped<IDomainEventHandler<DomainEvents.PaymentProcessed>, PaymentProcessedHandler>();
builder.Services.AddScoped<IDomainEventHandler<DomainEvents.AlertTriggered>, AlertTriggeredHandler>();
builder.Services.AddScoped<IDomainEventHandler<DomainEvents.ReadingProcessed>, ReadingProcessedHandler>();

// ---------------------------------------------------------------------- CORS
// Tres origenes: las dos aplicaciones desplegadas y el entorno de desarrollo.
// Si falta alguno, esa aplicacion falla con error de CORS y parece que el
// backend esta caido.
var allowedOrigins = (builder.Configuration["AllowedOrigins"]
                      ?? Environment.GetEnvironmentVariable("ALLOWED_ORIGINS")
                      ?? "https://sems-web-application.vercel.app,https://sems-diseno-web.vercel.app,http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// -------------------------------------------------------------- documentacion
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "SEMS API",
        Version = "v1",
        Description = "Smart Energy Management System. Monolito modular: un bounded context por modulo."
    });

    var xml = Path.Combine(AppContext.BaseDirectory,
        $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml))
    {
        options.IncludeXmlComments(xml);
    }

    // Nombre de esquema con el tipo contenedor delante: "EnergyResourcesAlertResponse"
    // en vez de "AlertResponse".
    //
    // Por defecto Swashbuckle nombra los esquemas solo con Type.Name, y aqui hay
    // dos records distintos llamados AlertResponse (uno en Energy y otro en
    // Alerts). Al chocar, la generacion entera aborta y /swagger/v1/swagger.json
    // devuelve 500: la pagina de Swagger carga pero se queda vacia.
    options.CustomSchemaIds(type =>
    {
        var parts = new List<string>();
        for (var t = type; t is not null; t = t.DeclaringType)
        {
            parts.Insert(0, t.Name.Split('`')[0]);
        }

        var name = string.Concat(parts);
        if (type.IsGenericType)
        {
            name += "Of" + string.Concat(type.GetGenericArguments().Select(a => a.Name.Split('`')[0]));
        }

        return name;
    });

    // Boton "Authorize" de Swagger. Sin esto no hay forma de probar los
    // endpoints desde la pagina, porque todos exigen sesion.
    var bearer = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Pegar aqui el token que devuelve /api/v1/auth/login (solo el token, sin \"Bearer\").",
        Reference = new Microsoft.OpenApi.Models.OpenApiReference
        {
            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    options.AddSecurityDefinition("Bearer", bearer);
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        [bearer] = Array.Empty<string>()
    });
});

// ------------------------------------------------------------------ seguridad
var jwtSecret = builder.Configuration["Security:Jwt:Secret"]
                ?? Environment.GetEnvironmentVariable("JWT_SECRET")
                ?? "replace_with_minimum_32_chars_secret_key_for_hs256";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Politica de respaldo: TODO endpoint exige sesion salvo el que se marque
    // explicitamente con [AllowAnonymous].
    //
    // Se hace asi, y no poniendo [Authorize] controlador por controlador,
    // porque el olvido tiene consecuencias opuestas: si falta un [Authorize] el
    // endpoint queda abierto y nadie se entera; si falta un [AllowAnonymous] el
    // endpoint devuelve 401 y se ve en la primera prueba.
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ------------------------------------------------------------- observabilidad
builder.Services.AddHealthChecks()
    .AddCheck<Sems.Api.Shared.Health.DatabaseHealthCheck>(
        "database",
        tags: new[] { "ready" });

var app = builder.Build();

// El middleware de errores va primero para capturar todo lo que ocurra debajo.
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "SEMS API v1"));

app.UseCors();
app.UseHttpMetrics();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// /health responde a "el proceso esta vivo": sin comprobaciones, para que el
// proveedor no reinicie el contenedor cuando lo que falla es la base de datos.
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

// /health/ready responde a "puede atender peticiones", y ahi si mira la base de
// datos. Es el que hay que consultar para saber por que fallan los endpoints.
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();
// /metrics para el monitoreo continuo del Cap. VII
app.MapMetrics("/metrics").AllowAnonymous();

// --------------------------------------------------- preparacion de la base
// Crea el esquema si no existe y carga los planes por defecto.
//
// La creacion del esquema es imprescindible y hay que pedirla explicitamente:
// la version en Java lo resuelve con spring.jpa.hibernate.ddl-auto=update, pero
// EF Core no crea nada por su cuenta. Sin esta llamada la aplicacion arranca
// perfectamente, el health check responde "Healthy", y luego TODA consulta
// falla con 500 porque las tablas no existen.
if (!string.IsNullOrWhiteSpace(connectionString))
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;

    // Deja en el log a que base se esta conectando, SIN la contrasena.
    //
    // Sirve para no depender de adivinar que hay en el panel del hosting: un
    // error de conexion no dice si el problema es el host, la base o el
    // usuario, y las variables no se pueden leer desde fuera. La contrasena
    // nunca se registra; lo demas no es secreto (Supabase lo muestra en su
    // propia pantalla de conexion).
    try
    {
        var datos = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
        app.Logger.LogInformation(
            "Conexion configurada -> Host={Host} Puerto={Port} Base={Database} Usuario={Username} Ssl={SslMode}",
            datos.Host, datos.Port, datos.Database, datos.Username, datos.SslMode);

        if (string.IsNullOrWhiteSpace(datos.Database))
        {
            app.Logger.LogError(
                "Falta 'Database=postgres' en DATABASE_URL. Sin eso Npgsql no sabe a que base conectarse.");
        }

        // El pooler compartido de Supabase atiende a muchos proyectos en el
        // mismo host y averigua cual es el tuyo por el sufijo del usuario
        // (postgres.<ref-del-proyecto>). Sin el punto corta la conexion con
        // "(ENOIDENTIFIER) no tenant identifier provided", que no se parece en
        // nada a "te falta el sufijo del usuario".
        if (datos.Host?.Contains("pooler.supabase.com", StringComparison.OrdinalIgnoreCase) == true
            && datos.Username?.Contains('.') != true)
        {
            app.Logger.LogError(
                "El usuario '{Username}' no lleva el sufijo del proyecto. Con el pooler de Supabase tiene que ser postgres.<ref-del-proyecto>.",
                datos.Username);
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "DATABASE_URL no tiene un formato valido de Npgsql");
    }

    try
    {
        var db = services.GetRequiredService<SemsDbContext>();

        // Migraciones, NO EnsureCreated().
        //
        // EnsureCreated() se rinde en silencio si la base de datos contiene
        // cualquier tabla, y da por hecho que ya esta montada. En Supabase eso
        // siempre se cumple: la base "postgres" viene de fabrica con los
        // esquemas auth, storage y realtime. El resultado es que arranca sin
        // quejarse, no crea ni una tabla, y luego cada peticion falla con
        // 'relation "iam_users" does not exist'.
        //
        // Las migraciones llevan su propio registro en __EFMigrationsHistory,
        // asi que no les importa lo que haya alrededor. Y ademas permiten
        // cambiar el modelo mas adelante, cosa que EnsureCreated tampoco hace.
        await db.Database.MigrateAsync();
        app.Logger.LogInformation("Migraciones aplicadas: el esquema esta al dia");
    }
    catch (Exception ex)
    {
        // Se registra como error, no como aviso: sin esquema la aplicacion no
        // sirve para nada y el log tiene que dejarlo claro.
        app.Logger.LogError(ex, "No se pudieron aplicar las migraciones de la base de datos");
    }

    try
    {
        await services.GetRequiredService<PlanSeeder>().SeedAsync();
    }
    catch (Exception ex)
    {
        // Esto si es recuperable: sin planes la aplicacion funciona, solo que
        // la pantalla de suscripcion sale vacia.
        app.Logger.LogWarning(ex, "No se pudieron cargar los planes por defecto");
    }
}
else
{
    app.Logger.LogWarning("DATABASE_URL no esta configurada: la aplicacion arranca sin base de datos");
}

app.Run();
