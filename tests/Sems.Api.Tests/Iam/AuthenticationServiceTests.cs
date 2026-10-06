using System.Security.Cryptography;
using System.Text;
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
/// Alta, inicio de sesion y recuperacion de contrasena, con el repositorio, el
/// hashing, la emision del token y el publicador de eventos simulados.
/// </summary>
public class AuthenticationServiceTests
{
    private const string Email = "owner@energix.test";
    private const string Password = "SecurePass123";

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHashingService _hashing = Substitute.For<IPasswordHashingService>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IUserAuthTokenRepository _authTokens = Substitute.For<IUserAuthTokenRepository>();
    private readonly IIamEventPublisher _events = Substitute.For<IIamEventPublisher>();
    private readonly AuthenticationService _service;
    private readonly AccountRecoveryService _recovery;

    public AuthenticationServiceTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // La siembra de datos de demostracion corre en segundo plano y
                // no forma parte de lo que se prueba aqui.
                ["Seeding:DemoData"] = "false",
                ["Security:RequireVerification"] = "false"
            })
            .Build();

        _users.SaveAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<User>());
        _refreshTokens.SaveAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<RefreshToken>());
        _authTokens.SaveAsync(Arg.Any<UserAuthToken>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<UserAuthToken>());
        _hashing.Hash(Arg.Any<string>()).Returns(call => "hash:" + call.Arg<string>());
        _hashing.Matches(Arg.Any<string>(), Arg.Any<string>())
            .Returns(call => call.ArgAt<string>(1) == "hash:" + call.ArgAt<string>(0));
        _tokens.GenerateToken(Arg.Any<User>()).Returns("access-token");

        var authTokenService = new AuthTokenService(_refreshTokens, _authTokens, configuration);
        var bus = Substitute.For<IDomainEventBus>();

        _service = new AuthenticationService(_users, _hashing, _tokens, authTokenService, _events,
            configuration,
            new DeviceCommandService(Substitute.For<IDeviceRepository>(),
                Substitute.For<IDeviceBindingRepository>(),
                Substitute.For<IDeviceConfigurationRepository>(),
                Substitute.For<IDeviceEventRepository>(), Substitute.For<ISiteDirectory>(), bus),
            new EnergyCommandService(Substitute.For<IEnergyMeterRepository>(),
                Substitute.For<IEnergyReadingRepository>(),
                Substitute.For<IConsumptionAlertRepository>(), Substitute.For<IUserGoalRepository>(),
                Substitute.For<IEnergyPricingProvider>(), bus),
            new OrganizationCommandService(Substitute.For<IOrganizationRepository>(),
                Substitute.For<ISiteRepository>(), Substitute.For<IZoneRepository>(),
                Substitute.For<IMembershipRepository>()),
            Substitute.For<IServiceProvider>());

        _recovery = new AccountRecoveryService(_users, _hashing, authTokenService, _service, _events,
            NullLogger<AccountRecoveryService>.Instance);
    }

    private User ExistingUser()
    {
        var user = User.Register(new EmailAddress(Email), "hash:" + Password, RoleName.STAFF, false);
        _users.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns(user);
        _users.ExistsByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns(true);
        return user;
    }

    // ---------------------------------------------------------------- registro

    [Fact]
    public async Task RegisterAsync_NewEmail_SavesPasswordHashAndPublishesUserRegisteredAsStaff()
    {
        var session = await _service.RegisterAsync(Email, Password, role: null);

        await _users.Received(1).SaveAsync(
            Arg.Is<User>(u => u.EmailAddress == Email && u.PasswordHash == "hash:" + Password
                              && u.Role == RoleName.STAFF),
            Arg.Any<CancellationToken>());
        _events.Received(1).PublishUserRegistered(session.UserId, Email, "STAFF");
        Assert.Equal("access-token", session.Token);
        Assert.False(string.IsNullOrWhiteSpace(session.RefreshToken));
    }

    [Fact]
    public async Task RegisterAsync_ExistingEmail_ThrowsConflictAndSavesNothing()
    {
        ExistingUser();

        var error = await Assert.ThrowsAsync<AppException>(
            () => _service.RegisterAsync(Email, "AnotherPass456", null));

        Assert.Equal(ErrorCode.CONFLICT, error.Code);
        await _users.DidNotReceive().SaveAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        _events.DidNotReceiveWithAnyArgs().PublishUserRegistered(default, default!, default!);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("1234567")]
    public async Task RegisterAsync_NullOrShorterThanEightCharactersPassword_ThrowsValidationError(
        string? password)
    {
        var error = await Assert.ThrowsAsync<AppException>(
            () => _service.RegisterAsync(Email, password, null));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
        await _users.DidNotReceive().SaveAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("RESIDENT")]
    [InlineData("resident")]
    public async Task RegisterAsync_ResidentRoleOfThePreviousSegment_ThrowsValidationError(string role)
    {
        var error = await Assert.ThrowsAsync<AppException>(
            () => _service.RegisterAsync(Email, Password, role));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
    }

    // -------------------------------------------------------- inicio de sesion

    [Fact]
    public async Task LoginAsync_ValidCredentials_IssuesSessionAndPublishesUserLoggedIn()
    {
        var user = ExistingUser();

        var session = await _service.LoginAsync(Email, Password);

        Assert.Equal(user.UserId, session.UserId);
        Assert.Equal("access-token", session.Token);
        _events.Received(1).PublishUserLoggedIn(user.UserId, Email);
    }

    [Fact]
    public async Task LoginAsync_WrongPasswordOrUnknownEmail_ThrowsTheSameUnauthorizedError()
    {
        ExistingUser();

        var wrongPassword = await Assert.ThrowsAsync<AppException>(
            () => _service.LoginAsync(Email, "WrongPass999"));
        var unknownEmail = await Assert.ThrowsAsync<AppException>(
            () => _service.LoginAsync("nobody@energix.test", Password));

        Assert.Equal(ErrorCode.UNAUTHORIZED, wrongPassword.Code);
        Assert.Equal(ErrorCode.UNAUTHORIZED, unknownEmail.Code);
        Assert.Equal("Invalid credentials", wrongPassword.Message);
        Assert.Equal(wrongPassword.Message, unknownEmail.Message);
        _events.DidNotReceiveWithAnyArgs().PublishUserLoggedIn(default, default!);
    }

    // ------------------------------------------------------------ recuperacion

    [Fact]
    public async Task ForgotPasswordAsync_UnknownEmail_EndsWithoutIssuingTokenOrEvent()
    {
        await _recovery.ForgotPasswordAsync("nobody@energix.test");

        await _authTokens.DidNotReceive().SaveAsync(Arg.Any<UserAuthToken>(), Arg.Any<CancellationToken>());
        _events.DidNotReceiveWithAnyArgs().PublishPasswordResetRequested(default, default!, default!);
    }

    [Fact]
    public async Task ForgotPasswordAsync_ExistingEmail_PublishesPasswordResetRequestedBeforeSavingTheToken()
    {
        var user = ExistingUser();
        string? publishedToken = null;
        _events.When(e => e.PublishPasswordResetRequested(user.UserId, Email, Arg.Any<string>()))
            .Do(call => publishedToken = call.ArgAt<string>(2));

        await _recovery.ForgotPasswordAsync(Email);

        // El bus despacha al confirmar una escritura: publicado despues del
        // ultimo guardado, el evento se quedaria en cola y el correo no saldria.
        Received.InOrder(() =>
        {
            _events.PublishPasswordResetRequested(user.UserId, Email, Arg.Any<string>());
            _authTokens.SaveAsync(Arg.Any<UserAuthToken>(), Arg.Any<CancellationToken>());
        });

        // El evento lleva el valor en claro del mismo token cuyo resumen se guarda.
        await _authTokens.Received(1).SaveAsync(
            Arg.Is<UserAuthToken>(t => t.Purpose == UserAuthToken.PurposePasswordReset
                                       && t.TokenHash == Sha256(publishedToken!)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidLink_ChangesThePasswordAndClosesEverySession()
    {
        var user = ExistingUser();
        _users.FindByIdAsync(user.UserId, Arg.Any<CancellationToken>()).Returns(user);
        string? link = null;
        _events.When(e => e.PublishPasswordResetRequested(user.UserId, Email, Arg.Any<string>()))
            .Do(call => link = call.ArgAt<string>(2));
        UserAuthToken? stored = null;
        _authTokens.SaveAsync(Arg.Do<UserAuthToken>(t => stored = t), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<UserAuthToken>());
        _authTokens.FindByHashAndPurposeAsync(Arg.Any<string>(), UserAuthToken.PurposePasswordReset,
            Arg.Any<CancellationToken>()).Returns(_ => stored);
        await _recovery.ForgotPasswordAsync(Email);

        await _recovery.ResetPasswordAsync(link!, "NewSecurePass456");

        Assert.Equal("hash:NewSecurePass456", user.PasswordHash);
        await _refreshTokens.Received(1).RevokeAllForUserAsync(user.UserId, Arg.Any<CancellationToken>());
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
