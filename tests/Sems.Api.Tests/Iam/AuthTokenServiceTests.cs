using Microsoft.Extensions.Configuration;
using Sems.Api.Modules.Iam.Application;
using Sems.Api.Modules.Iam.Domain.Model;
using Sems.Api.Modules.Iam.Domain.Repositories;
using Sems.Api.Shared.Errors;
using Xunit;

namespace Sems.Api.Tests.Iam;

/// <summary>
/// Garantias de seguridad de los tokens opacos.
///
/// <para>Se prueban contra repositorios en memoria porque lo que interesa
/// verificar no es el acceso a datos sino tres decisiones que son invisibles
/// mirando la API: que en base de datos queda el resumen y no el token, que un
/// enlace de un solo uso no sirve dos veces, y que refrescar rota el token.</para>
/// </summary>
public class AuthTokenServiceTests
{
    private readonly InMemoryRefreshTokens _refreshTokens = new();
    private readonly InMemoryAuthTokens _authTokens = new();
    private readonly AuthTokenService _service;

    public AuthTokenServiceTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:Jwt:RefreshExpirationDays"] = "30"
            })
            .Build();

        _service = new AuthTokenService(_refreshTokens, _authTokens, configuration);
    }

    [Fact]
    public async Task IssueRefreshTokenAsync_AnyUser_StoresItsSha256HashNeverTheRawValue()
    {
        var userId = Guid.NewGuid();

        var raw = await _service.IssueRefreshTokenAsync(userId);

        var stored = Assert.Single(_refreshTokens.Items);
        Assert.NotEqual(raw, stored.TokenHash);
        Assert.Equal(64, stored.TokenHash.Length);            // SHA-256 en hexadecimal
        Assert.DoesNotContain(raw, stored.TokenHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IssueRefreshTokenAsync_TwoConsecutiveIssues_ReturnDifferentValues()
    {
        var userId = Guid.NewGuid();

        var first = await _service.IssueRefreshTokenAsync(userId);
        var second = await _service.IssueRefreshTokenAsync(userId);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task ConsumeRefreshTokenAsync_UsedToken_RotatesItAndRejectsTheSecondUse()
    {
        var userId = Guid.NewGuid();
        var raw = await _service.IssueRefreshTokenAsync(userId);

        var owner = await _service.ConsumeRefreshTokenAsync(raw);
        Assert.Equal(userId, owner);

        // El mismo valor ya no vale: si alguien lo robo, el robo se hace visible.
        var error = await Assert.ThrowsAsync<AppException>(
            () => _service.ConsumeRefreshTokenAsync(raw));
        Assert.Equal(ErrorCode.UNAUTHORIZED, error.Code);
    }

    [Fact]
    public async Task ConsumeRefreshTokenAsync_UnknownToken_ThrowsUnauthorized()
    {
        var error = await Assert.ThrowsAsync<AppException>(
            () => _service.ConsumeRefreshTokenAsync("no-existe"));

        Assert.Equal(ErrorCode.UNAUTHORIZED, error.Code);
    }

    [Fact]
    public async Task RevokeAsync_WithoutToken_RevokesEverySessionOfTheUser()
    {
        var userId = Guid.NewGuid();
        await _service.IssueRefreshTokenAsync(userId);
        await _service.IssueRefreshTokenAsync(userId);

        await _service.RevokeAsync(userId, null);

        Assert.All(_refreshTokens.Items, t => Assert.True(t.Revoked));
    }

    [Fact]
    public async Task RevokeAsync_WithToken_RevokesOnlyThatSession()
    {
        var userId = Guid.NewGuid();
        var closed = await _service.IssueRefreshTokenAsync(userId);
        var open = await _service.IssueRefreshTokenAsync(userId);

        await _service.RevokeAsync(userId, closed);

        await Assert.ThrowsAsync<AppException>(() => _service.ConsumeRefreshTokenAsync(closed));
        Assert.Equal(userId, await _service.ConsumeRefreshTokenAsync(open));
    }

    [Fact]
    public async Task ConsumeSingleUseAsync_PasswordResetLink_WorksOnlyOnce()
    {
        var userId = Guid.NewGuid();
        var raw = await _service.IssuePasswordResetTokenAsync(userId);

        var owner = await _service.ConsumeSingleUseAsync(raw, UserAuthToken.PurposePasswordReset);
        Assert.Equal(userId, owner);

        // Un correo reenviado o archivado deja de servir tras el primer uso.
        var error = await Assert.ThrowsAsync<AppException>(
            () => _service.ConsumeSingleUseAsync(raw, UserAuthToken.PurposePasswordReset));
        Assert.Equal(ErrorCode.UNAUTHORIZED, error.Code);
    }

    [Fact]
    public async Task ConsumeSingleUseAsync_VerificationTokenUsedAsResetLink_IsRejected()
    {
        var userId = Guid.NewGuid();
        var raw = await _service.IssueVerificationTokenAsync(userId);

        // Verificar la cuenta no puede convertirse en cambiar la contrasena.
        var error = await Assert.ThrowsAsync<AppException>(
            () => _service.ConsumeSingleUseAsync(raw, UserAuthToken.PurposePasswordReset));
        Assert.Equal(ErrorCode.UNAUTHORIZED, error.Code);

        var owner = await _service.ConsumeSingleUseAsync(raw, UserAuthToken.PurposeVerification);
        Assert.Equal(userId, owner);
    }

    [Fact]
    public async Task IssuePasswordResetTokenAsync_WithCallback_HandsTheRawValueBeforeSavingIt()
    {
        string? handed = null;
        var savedBeforeCallback = true;

        var raw = await _service.IssuePasswordResetTokenAsync(Guid.NewGuid(), value =>
        {
            handed = value;
            savedBeforeCallback = _authTokens.Items.Count > 0;
        });

        Assert.Equal(raw, handed);
        Assert.False(savedBeforeCallback);
        Assert.Single(_authTokens.Items);
    }

    // ------------------------------------------------------- dobles de prueba

    private sealed class InMemoryRefreshTokens : IRefreshTokenRepository
    {
        public List<RefreshToken> Items { get; } = new();

        public Task<RefreshToken> SaveAsync(RefreshToken token, CancellationToken ct = default)
        {
            if (!Items.Contains(token))
            {
                Items.Add(token);
            }
            return Task.FromResult(token);
        }

        public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == tokenHash));

        public Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
        {
            foreach (var token in Items.Where(t => t.UserId == userId))
            {
                token.Revoke();
            }
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryAuthTokens : IUserAuthTokenRepository
    {
        public List<UserAuthToken> Items { get; } = new();

        public Task<UserAuthToken> SaveAsync(UserAuthToken token, CancellationToken ct = default)
        {
            if (!Items.Contains(token))
            {
                Items.Add(token);
            }
            return Task.FromResult(token);
        }

        public Task<UserAuthToken?> FindByHashAndPurposeAsync(string tokenHash, string purpose,
            CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(
                t => t.TokenHash == tokenHash && t.Purpose == purpose));
    }
}
