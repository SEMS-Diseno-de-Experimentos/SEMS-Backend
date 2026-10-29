using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sems.Api.Modules.Devices.Application;
using Sems.Api.Modules.Devices.Domain.Repositories;
using Sems.Api.Modules.Devices.Domain.Services;
using Sems.Api.Modules.Energy.Application;
using Sems.Api.Modules.Energy.Domain.Repositories;
using Sems.Api.Modules.Energy.Domain.Services;
using Sems.Api.Modules.Iam.Application;
using Sems.Api.Modules.Iam.Domain.Model;
using Sems.Api.Modules.Iam.Domain.Repositories;
using Sems.Api.Modules.Iam.Domain.Services;
using Sems.Api.Modules.Organizations.Application;
using Sems.Api.Modules.Organizations.Domain.Repositories;
using Sems.Api.Shared.Errors;
using Sems.Api.Shared.Events;
using Xunit;

namespace Sems.Api.Tests.Iam;

/// <summary>
/// Entrada con una cuenta de Google.
/// </summary>
/// <remarks>
/// Lo que se comprueba aqui es la puerta de seguridad. Un token de Google es
/// valido de verdad y esta firmado por Google, y aun asi puede no servir aqui:
/// si se emitio para otra aplicacion, aceptarlo dejaria entrar a cualquiera que
/// tenga una cuenta en esa otra aplicacion.
/// </remarks>
public class GoogleSignInTests
{
    private const string ClientId = "123456789-energix.apps.googleusercontent.com";
    private const string Email = "manager@energix.test";

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHashingService _hashing = Substitute.For<IPasswordHashingService>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly IIamEventPublisher _events = Substitute.For<IIamEventPublisher>();
    private readonly IGoogleTokenVerifier _google = Substitute.For<IGoogleTokenVerifier>();

    private AuthenticationService Servicio(string clientId = ClientId)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seeding:DemoData"] = "false",
                ["Security:RequireVerification"] = "false",
                ["Security:Google:ClientId"] = clientId
            })
            .Build();

        var bus = Substitute.For<IDomainEventBus>();
        return new AuthenticationService(_users, _hashing, _tokens,
            new AuthTokenService(Substitute.For<IRefreshTokenRepository>(),
                Substitute.For<IUserAuthTokenRepository>(), configuration),
            _events, configuration,
            new DeviceCommandService(Substitute.For<IDeviceRepository>(),
                Substitute.For<IDeviceBindingRepository>(),
                Substitute.For<IDeviceConfigurationRepository>(),
                Substitute.For<IDeviceEventRepository>(), Substitute.For<ISiteDirectory>(), bus),
            new EnergyCommandService(Substitute.For<IEnergyMeterRepository>(),
                Substitute.For<IEnergyReadingRepository>(),
                Substitute.For<IConsumptionAlertRepository>(),
                Substitute.For<IUserGoalRepository>(),
                Substitute.For<IEnergyPricingProvider>(),
                Substitute.For<ISiteCatalog>(), bus),
            new OrganizationCommandService(Substitute.For<IOrganizationRepository>(),
                Substitute.For<ISiteRepository>(), Substitute.For<IZoneRepository>(),
                Substitute.For<IMembershipRepository>()),
            Substitute.For<IServiceProvider>(), _google);
    }

    public GoogleSignInTests()
    {
        _hashing.Hash(Arg.Any<string>()).Returns(call => "hash:" + call.Arg<string>());
        _tokens.GenerateToken(Arg.Any<User>()).Returns("access-token");
        _users.SaveAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<User>());
        _users.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);
    }

    private void GoogleDice(string audience = ClientId, string email = Email,
        bool verificado = true, string? nombre = "Alexis Quispe") =>
        _google.VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new IGoogleTokenVerifier.GoogleIdentity(audience, email, verificado, nombre));

    // ------------------------------------------------------- el camino feliz

    [Fact]
    public async Task SignInWithGoogleAsync_FirstTime_CreatesTheAccountWithTheGoogleName()
    {
        GoogleDice();

        var sesion = await Servicio().SignInWithGoogleAsync("un-token");

        Assert.Equal(Email, sesion.EmailAddress);
        Assert.Equal("Alexis Quispe", sesion.FullName);
        Assert.Equal(new[] { "STAFF" }, sesion.Roles);
        await _users.Received(1).SaveAsync(
            Arg.Is<User>(u => u.EmailAddress == Email && !u.IsPending), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignInWithGoogleAsync_AccountAlreadyExists_SignsInWithoutCreatingAnother()
    {
        var existente = User.Register(new EmailAddress(Email), "hash:x", RoleName.STAFF, false,
            "Nombre Guardado");
        _users.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns(existente);
        GoogleDice();

        var sesion = await Servicio().SignInWithGoogleAsync("un-token");

        Assert.Equal(existente.UserId, sesion.UserId);
        // El nombre que ya tenia manda sobre el de Google: el usuario lo
        // cambio a proposito en su perfil.
        Assert.Equal("Nombre Guardado", sesion.FullName);
        await _users.DidNotReceive().SaveAsync(Arg.Is<User>(u => u.UserId != existente.UserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignInWithGoogleAsync_AccountPendingVerification_GoogleVouchesForIt()
    {
        // Se registro a mano, no verifico el correo, y ahora entra por Google.
        // Google acaba de comprobar ese correo: pedirselo otra vez sobra.
        var pendiente = User.Register(new EmailAddress(Email), "hash:x", RoleName.STAFF, true);
        Assert.True(pendiente.IsPending);
        _users.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns(pendiente);
        GoogleDice();

        await Servicio().SignInWithGoogleAsync("un-token");

        Assert.False(pendiente.IsPending);
    }

    // --------------------------------------------------- la puerta de seguridad

    [Fact]
    public async Task SignInWithGoogleAsync_TokenIssuedForAnotherApplication_IsRejected()
    {
        // Es la comprobacion que no se ve venir: el token esta firmado por
        // Google y es autentico, pero lo emitio otra aplicacion. Sin esto,
        // cualquiera con una cuenta en esa otra entraria aqui.
        GoogleDice(audience: "999-otra-app.apps.googleusercontent.com");

        var error = await Assert.ThrowsAsync<AppException>(
            () => Servicio().SignInWithGoogleAsync("un-token"));

        Assert.Equal(ErrorCode.UNAUTHORIZED, error.Code);
        await _users.DidNotReceive().SaveAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignInWithGoogleAsync_EmailNotVerifiedByGoogle_IsRejected()
    {
        GoogleDice(verificado: false);

        var error = await Assert.ThrowsAsync<AppException>(
            () => Servicio().SignInWithGoogleAsync("un-token"));

        Assert.Equal(ErrorCode.UNAUTHORIZED, error.Code);
        await _users.DidNotReceive().SaveAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignInWithGoogleAsync_GoogleDoesNotRecognizeTheToken_IsRejected()
    {
        _google.VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((IGoogleTokenVerifier.GoogleIdentity?)null);

        var error = await Assert.ThrowsAsync<AppException>(
            () => Servicio().SignInWithGoogleAsync("un-token"));

        Assert.Equal(ErrorCode.UNAUTHORIZED, error.Code);
    }

    [Fact]
    public async Task SignInWithGoogleAsync_WithoutAClientIdConfigured_SaysSoInsteadOfLettingAnyoneIn()
    {
        GoogleDice();

        var error = await Assert.ThrowsAsync<AppException>(
            () => Servicio(clientId: string.Empty).SignInWithGoogleAsync("un-token"));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
        Assert.Contains("not configured", error.Message);
    }

    [Fact]
    public async Task SignInWithGoogleAsync_WithoutAToken_IsRejected()
    {
        var error = await Assert.ThrowsAsync<AppException>(
            () => Servicio().SignInWithGoogleAsync("   "));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
    }
}
