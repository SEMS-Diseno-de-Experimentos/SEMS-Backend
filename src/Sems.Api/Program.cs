using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Sems.Api.Modules.Devices.Application;
using Sems.Api.Modules.Devices.Domain.Repositories;
using Sems.Api.Modules.Devices.Infrastructure;
using Sems.Api.Modules.Energy.Application;
using Sems.Api.Modules.Energy.Domain.Repositories;
using Sems.Api.Modules.Energy.Domain.Services;
using Sems.Api.Modules.Energy.Infrastructure;
using Sems.Api.Modules.Analytics.Application;
using Sems.Api.Modules.Analytics.Domain.Repositories;
using Sems.Api.Modules.Analytics.Infrastructure;
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
builder.Services.AddScoped<AnalyticsService>();

builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddSingleton<SubscriptionManager>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<PlanSeeder>();

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

builder.Services.AddAuthorization();

// ------------------------------------------------------------- observabilidad
builder.Services.AddHealthChecks();

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
app.MapHealthChecks("/health");
// /metrics para el monitoreo continuo del Cap. VII
app.MapMetrics("/metrics");

// Carga los planes por defecto si la base esta vacia.
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<PlanSeeder>();
    try
    {
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        // Sin base de datos configurada la aplicacion debe arrancar igual: el
        // health check lo reportara y el fallo queda en el log.
        app.Logger.LogWarning(ex, "No se pudieron cargar los planes por defecto");
    }
}

app.Run();
