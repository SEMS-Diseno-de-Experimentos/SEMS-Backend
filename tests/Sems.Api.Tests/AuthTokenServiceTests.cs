using Microsoft.Extensions.Configuration;
using Sems.Api.Modules.Iam.Application;
using Sems.Api.Modules.Iam.Domain.Model;
using Sems.Api.Modules.Iam.Domain.Repositories;
using Sems.Api.Shared.Errors;
using Xunit;

namespace Sems.Api.Tests;

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
    public async Task El_token_nunca_se_guarda_en_claro()
    {
        var userId = Guid.NewGuid();

        var raw = await _service.IssueRefreshTokenAsync(userId);

        var stored = Assert.Single(_refreshTokens.Items);
        Assert.NotEqual(raw, stored.TokenHash);
        Assert.Equal(64, stored.TokenHash.Length);            // SHA-256 en hexadecimal
        Assert.DoesNotContain(raw, stored.TokenHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dos_emisiones_seguidas_no_repiten_el_valor()
    {
        var userId = Guid.NewGuid();

        var first = await _service.IssueRefreshTokenAsync(userId);
        var second = await _service.IssueRefreshTokenAsync(userId);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task Refrescar_rota_el_token_usado()
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
    public async Task Un_token_de_refresco_desconocido_se_rechaza()
    {
        var error = await Assert.ThrowsAsync<AppException>(
            () => _service.ConsumeRefreshTokenAsync("no-existe"));

        Assert.Equal(ErrorCode.UNAUTHORIZED, error.Code);
    }

    [Fact]
    public async Task Cerrar_sesion_sin_token_revoca_todas_las_del_usuario()
    {
        var userId = Guid.NewGuid();
        await _service.IssueRefreshTokenAsync(userId);
        await _service.IssueRefreshTokenAsync(userId);

        await _service.RevokeAsync(userId, null);

        Assert.All(_refreshTokens.Items, t => Assert.True(t.Revoked));
    }

    [Fact]
    public async Task El_enlace_de_recuperacion_solo_sirve_una_vez()
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
    public async Task Un_token_no_sirve_para_un_proposito_distinto()
    {
        var userId = Guid.NewGuid();
        var raw = await _service.IssueVerificationTokenAsync(userId);

        // Verificar la cuenta no puede convertirse en cambiar la contrasena.
        await Assert.ThrowsAsync<AppException>(
            () => _service.ConsumeSingleUseAsync(raw, UserAuthToken.PurposePasswordReset));

        var owner = await _service.ConsumeSingleUseAsync(raw, UserAuthToken.PurposeVerification);
        Assert.Equal(userId, owner);
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
