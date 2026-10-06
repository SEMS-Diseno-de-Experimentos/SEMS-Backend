using Sems.Api.Modules.Iam.Domain.Model;
using Sems.Api.Modules.Iam.Domain.Repositories;
using Sems.Api.Modules.Iam.Domain.Services;
using Sems.Api.Shared.Errors;

namespace Sems.Api.Modules.Iam.Application;

/// <summary>Sesion entregada tras autenticarse.</summary>
public sealed record SessionResult(string Token, string RefreshToken, Guid UserId,
    string EmailAddress, List<string> Roles);

/// <summary>
/// Alta e inicio de sesion.
///
/// <para>Orquesta repositorios, servicios de dominio y el publicador de eventos,
/// pero no contiene reglas de negocio: esas viven en el dominio.</para>
/// </summary>
public sealed class AuthenticationService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHashingService _hashing;
    private readonly ITokenService _tokens;
    private readonly AuthTokenService _authTokens;
    private readonly IIamEventPublisher _events;
    private readonly bool _requireVerification;
    private readonly bool _seedDemoData;

    private readonly Sems.Api.Modules.Devices.Application.DeviceCommandService _devices;
    private readonly Sems.Api.Modules.Energy.Application.EnergyCommandService _energy;
    private readonly Sems.Api.Modules.Organizations.Application.OrganizationCommandService _organizations;

    private readonly IServiceProvider _serviceProvider;

    public AuthenticationService(IUserRepository users, IPasswordHashingService hashing,
        ITokenService tokens, AuthTokenService authTokens, IIamEventPublisher events,
        IConfiguration configuration,
        Sems.Api.Modules.Devices.Application.DeviceCommandService devices,
        Sems.Api.Modules.Energy.Application.EnergyCommandService energy,
        Sems.Api.Modules.Organizations.Application.OrganizationCommandService organizations,
        IServiceProvider serviceProvider)
    {
        _users = users;
        _hashing = hashing;
        _tokens = tokens;
        _authTokens = authTokens;
        _events = events;
        _requireVerification = string.Equals(
            configuration["Security:RequireVerification"]
            ?? Environment.GetEnvironmentVariable("REQUIRE_VERIFICATION"),
            "true", StringComparison.OrdinalIgnoreCase);
        _seedDemoData = Sems.Api.Shared.Persistence.SystemDataSeeder.DemoDataEnabled(configuration);
        _devices = devices;
        _energy = energy;
        _organizations = organizations;
        _serviceProvider = serviceProvider;
    }

    public async Task<SessionResult> RegisterAsync(string? emailAddress, string? password,
        string? role, CancellationToken ct = default)
    {
        var email = new EmailAddress(emailAddress);

        if (await _users.ExistsByEmailAsync(email.Value, ct))
        {
            throw AppException.Conflict("Email already exists");
        }
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw AppException.Validation("password must be at least 8 characters");
        }

        var user = await _users.SaveAsync(User.Register(email, _hashing.Hash(password),
            RoleNameExtensions.ToRoleName(role), _requireVerification), ct);

        _events.PublishUserRegistered(user.UserId, user.EmailAddress, user.Role.ToString());

        if (_requireVerification)
        {
            var verificationToken = await _authTokens.IssueVerificationTokenAsync(user.UserId, ct);
            _events.PublishVerificationRequested(user.UserId, user.EmailAddress, verificationToken);
        }

        if (_seedDemoData)
        {
            var sp = _serviceProvider;
            var uId = user.UserId;
            var e = user.EmailAddress;
            _ = Task.Run(async () => {
                using var scope = sp.CreateScope();
                var auth = scope.ServiceProvider.GetRequiredService<AuthenticationService>();
                try {
                    await auth.SeedDemoDataAsync(uId, e, CancellationToken.None);
                } catch (Exception ex) {
                    Console.WriteLine($"SEEDING ERROR: {ex}");
                }
            });
        }

        return await BuildSessionAsync(user, ct);
    }

    private async Task SeedDemoDataAsync(Guid userId, string email, CancellationToken ct)
    {
        var random = new Random();
        var types = new[] { "HVAC", "LIGHTING", "REFRIGERATION", "MACHINERY", "OTHER" };
        var brands = new[] { "Samsung", "LG", "Siemens", "General Electric", "Philips" };
        var models = new[] { "X100", "Pro V2", "EcoSmart", "Industrial", "Basic" };

        // 1. Create a default Organization and Site for this user
        var taxId = "20" + random.Next(100000000, 999999999).ToString();
        var (org, _) = await _organizations.RegisterAsync("Mi Hogar / Empresa", "Principal", taxId, "OTHER", userId, ct);
        var site = await _organizations.RegisterSiteAsync(org.OrganizationId, "SITE-01", "Sede Principal", "Av. Siempre Viva 123", "Lima", 120, 10, "BT5B", false, ct);

        // 2. Create a default Energy Meter
        var meter = await _energy.RegisterMeterAsync(
            userId.ToString(),
            $"METER-{random.Next(10000, 99999)}",
            "SmartMeter V1",
            "EOS",
            "Main Board",
            "1.0.0",
            10000.0,
            ct
        );

        var realNames = new[] { "Luces Pasadizo", "Refrigerador Principal", "Aire Acondicionado Sala", "Horno Industrial", "Servidor Rack 1", "TV Recepción", "Letrero Luminoso", "Cargador Coche Eléctrico", "Bomba de Agua" };
        var randomStatuses = new[] { "ACTIVE", "ACTIVE", "ACTIVE", "INACTIVE", "INACTIVE", "MAINTENANCE" };

        for (int i = 1; i <= 9; i++)
        {
            var deviceType = types[random.Next(types.Length)];
            var brand = brands[random.Next(brands.Length)];
            var model = models[random.Next(models.Length)];

            // RegisterAsync signature: externalCode, userId, siteId, zoneId, name, type, brand, model, protocol
            var device = await _devices.RegisterAsync(
                $"EXT-{userId.ToString().Substring(0, 4)}-{i}", 
                userId, 
                site.SiteId, 
                null, 
                realNames[i-1], 
                deviceType, 
                brand, 
                model, 
                "WIFI", 
                ct);
                
            var initialStatus = randomStatuses[random.Next(randomStatuses.Length)];
            if (initialStatus != "ACTIVE")
            {
                await _devices.ChangeStatusAsync(device.DeviceId, initialStatus, ct);
            }

            // Record some readings!
            double currentKwh = 100.0 + random.NextDouble() * 50.0;
            
            for (int day = 13; day >= 0; day--)
            {
                var dailyKwh = 5.0 + random.NextDouble() * 235.0; 
                var dailyPower = dailyKwh * 1000.0 / 5.0; // Reverse calculate power assuming 5 hours
                currentKwh += dailyKwh;
                
                await _energy.RecordReadingAsync(
                    userId.ToString(), 
                    meter.Id.ToString(), 
                    device.DeviceId.ToString(),
                    dailyPower, 
                    220.0, 
                    5.0 + random.NextDouble() * 5.0, 
                    60.0, 
                    dailyKwh, 
                    DateTime.UtcNow.AddDays(-day), 
                    initialStatus, 
                    "A", 
                    ct);
            }
                
            if (random.Next(100) < 30) // 30% chance for an alert
            {
                await _energy.RaiseAlertAsync(
                    userId.ToString(), 
                    device.DeviceId.ToString(), 
                    null, 
                    Sems.Api.Modules.Energy.Domain.Model.AlertType.high_consumption, 
                    Sems.Api.Modules.Energy.Domain.Model.AlertSeverity.high, 
                    1500.0, 
                    1800.0, 
                    $"Consumo anormal detectado de 1800W", 
                    ct);
            }
        }

        // Set a global monthly goal so alerts can trigger!
        await _energy.SetUserGoalAsync(userId.ToString(), 1500.0, ct);

        // Generate analytics rankings
        using var scope = _serviceProvider.CreateScope();
        var energyQueries = scope.ServiceProvider.GetRequiredService<Sems.Api.Modules.Energy.Application.EnergyQueryService>();
        var analytics = scope.ServiceProvider.GetRequiredService<Sems.Api.Modules.Analytics.Application.AnalyticsService>();

        var rankings = new List<Sems.Api.Modules.Analytics.Domain.Model.RankingItem>();
        int rank = 1;
        var deviceConsumptions = await energyQueries.ConsumptionsByUserAsync(userId.ToString(), ct);
        foreach (var dc in deviceConsumptions.OrderByDescending(x => x.TotalKwh).Take(5))
        {
            var r = new Sems.Api.Modules.Analytics.Domain.Model.RankingItem(
                rank++, 
                dc.DeviceId.ToString(), 
                dc.DeviceName, 
                dc.TotalKwh, 
                dc.CostEstimateSoles, 
                0, 
                "PEN"
            );
            rankings.Add(r);
        }
        
        await analytics.CreateRankingAsync(userId.ToString(), "monthly", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, rankings, ct);
    }

    public async Task<SessionResult> LoginAsync(string? emailAddress, string? password,
        CancellationToken ct = default)
    {
        var email = new EmailAddress(emailAddress);

        // Cuando el correo no existe O la contrasena es incorrecta se lanza el
        // MISMO error a proposito. Un mensaje distinto revelaria que correos
        // estan registrados.
        var user = await _users.FindByEmailAsync(email.Value, ct)
                   ?? throw AppException.Unauthorized("Invalid credentials");

        if (string.IsNullOrEmpty(password) || !_hashing.Matches(password, user.PasswordHash))
        {
            throw AppException.Unauthorized("Invalid credentials");
        }

        // Una cuenta sin verificar no entra. El mensaje si es explicito aqui
        // porque las credenciales ya se comprobaron: no filtra nada.
        if (_requireVerification && user.IsPending)
        {
            throw AppException.Unauthorized("Account is not verified yet");
        }

        _events.PublishUserLoggedIn(user.UserId, user.EmailAddress);
        return await BuildSessionAsync(user, ct);
    }

    /// <summary>Arma el par de tokens y la respuesta de sesion para un usuario.</summary>
    public async Task<SessionResult> BuildSessionAsync(User user, CancellationToken ct = default)
    {
        var accessToken = _tokens.GenerateToken(user);
        var refreshToken = await _authTokens.IssueRefreshTokenAsync(user.UserId, ct);

        return new SessionResult(accessToken, refreshToken, user.UserId, user.EmailAddress,
            new List<string> { user.Role.ToString() });
    }
}

/// <summary>
/// Verificacion de cuenta, recuperacion de contrasena, refresco y cierre de sesion.
///
/// <para>Se separa de <see cref="AuthenticationService"/> para que ese no siga
/// creciendo: alli viven el alta y el inicio de sesion; aqui, el ciclo de vida
/// de la credencial.</para>
/// </summary>
public sealed class AccountRecoveryService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHashingService _hashing;
    private readonly AuthTokenService _authTokens;
    private readonly AuthenticationService _authentication;
    private readonly IIamEventPublisher _events;
    private readonly ILogger<AccountRecoveryService> _logger;

    public AccountRecoveryService(IUserRepository users, IPasswordHashingService hashing,
        AuthTokenService authTokens, AuthenticationService authentication,
        IIamEventPublisher events, ILogger<AccountRecoveryService> logger)
    {
        _users = users;
        _hashing = hashing;
        _authTokens = authTokens;
        _authentication = authentication;
        _events = events;
        _logger = logger;
    }

    /// <summary>
    /// Entrega un par de tokens nuevo a partir de uno de refresco valido.
    ///
    /// <para>El de refresco se rota en el proceso: el anterior queda revocado.</para>
    /// </summary>
    public async Task<SessionResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var userId = await _authTokens.ConsumeRefreshTokenAsync(refreshToken, ct);
        var user = await _users.FindByIdAsync(userId, ct)
                   ?? throw AppException.Unauthorized("Invalid refresh token");

        return await _authentication.BuildSessionAsync(user, ct);
    }

    /// <summary>Cierra la sesion. Sin token concreto, cierra todas las del usuario.</summary>
    public Task LogoutAsync(Guid? userId, string? refreshToken, CancellationToken ct = default) =>
        _authTokens.RevokeAsync(userId, refreshToken, ct);

    /// <summary>Activa la cuenta y devuelve una sesion, para que el usuario entre directo.</summary>
    public async Task<SessionResult> VerifyAccountAsync(string token, CancellationToken ct = default)
    {
        var userId = await _authTokens.ConsumeSingleUseAsync(token,
            UserAuthToken.PurposeVerification, ct);

        var user = await _users.FindByIdAsync(userId, ct)
                   ?? throw AppException.NotFound("User not found");

        user.Activate();
        await _users.SaveAsync(user, ct);

        return await _authentication.BuildSessionAsync(user, ct);
    }

    /// <summary>
    /// Inicia la recuperacion de contrasena.
    ///
    /// <para><b>No revela si el correo existe.</b> El metodo termina en silencio
    /// cuando no hay cuenta asociada, y el controlador responde siempre lo
    /// mismo. Contestar distinto convertiria este endpoint en un verificador de
    /// correos registrados para cualquiera que lo consulte.</para>
    /// </summary>
    public async Task ForgotPasswordAsync(string? emailAddress, CancellationToken ct = default)
    {
        var email = new EmailAddress(emailAddress);
        var user = await _users.FindByEmailAsync(email.Value, ct);

        if (user is null)
        {
            _logger.LogInformation(
                "Recuperacion solicitada para un correo no registrado; no se envia nada");
            return;
        }

        // El evento se publica antes de guardar el token, no despues: el bus lo
        // entrega cuando esa escritura confirma. Publicado tras el ultimo
        // guardado se quedaba en cola y el enlace nunca llegaba por correo.
        await _authTokens.IssuePasswordResetTokenAsync(user.UserId,
            token => _events.PublishPasswordResetRequested(user.UserId, user.EmailAddress, token),
            ct);
    }

    /// <summary>
    /// Cambia la contrasena y cierra todas las sesiones abiertas.
    ///
    /// <para>Revocar los tokens es parte del caso de uso: si el usuario cambia la
    /// contrasena porque sospecha que alguien entro, dejar viva la sesion del
    /// intruso vaciaria de sentido la operacion.</para>
    /// </summary>
    public async Task ResetPasswordAsync(string token, string newPassword,
        CancellationToken ct = default)
    {
        var userId = await _authTokens.ConsumeSingleUseAsync(token,
            UserAuthToken.PurposePasswordReset, ct);

        var user = await _users.FindByIdAsync(userId, ct)
                   ?? throw AppException.NotFound("User not found");

        user.ChangePassword(_hashing.Hash(newPassword));
        await _users.SaveAsync(user, ct);

        await _authTokens.RevokeAsync(userId, null, ct);
    }
}
