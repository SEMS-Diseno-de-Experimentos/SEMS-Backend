using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Sems.Api.Modules.Iam.Domain.Model;
using Sems.Api.Modules.Iam.Infrastructure;
using Sems.Api.Shared.Errors;
using Xunit;

namespace Sems.Api.Tests.Iam;

/// <summary>
/// El agregado <see cref="User"/>, el value object <see cref="EmailAddress"/> y
/// los adaptadores de seguridad (BCrypt y JWT).
/// </summary>
public class IdentityDomainTests
{
    private const string Secret = "unit-test-jwt-secret-with-more-than-32-chars";

    // ----------------------------------------------------------------- correo

    [Theory]
    [InlineData("  Owner@Energix.TEST  ")]
    [InlineData("OWNER@ENERGIX.TEST")]
    public void EmailAddress_WithSpacesAndUppercase_IsNormalized(string raw)
    {
        Assert.Equal("owner@energix.test", new EmailAddress(raw).Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("owner")]
    [InlineData("owner@")]
    [InlineData("@energix.test")]
    [InlineData("owner@energix")]
    [InlineData("owner name@energix.test")]
    public void EmailAddress_InvalidFormat_ThrowsValidationError(string? raw)
    {
        var error = Assert.Throws<AppException>(() => new EmailAddress(raw));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
    }

    // ---------------------------------------------------------------- usuario

    [Fact]
    public void Register_WithVerificationRequired_StartsPendingUntilActivated()
    {
        var user = User.Register(new EmailAddress("owner@energix.test"), "hash", RoleName.STAFF,
            requireVerification: true);

        Assert.Equal(User.StatusPending, user.Status);
        Assert.True(user.IsPending);

        user.Activate();

        Assert.Equal(User.StatusActive, user.Status);
        Assert.False(user.IsPending);
    }

    [Fact]
    public void Register_WithoutVerification_StartsActive()
    {
        var user = User.Register(new EmailAddress("owner@energix.test"), "hash", RoleName.STAFF,
            requireVerification: false);

        Assert.Equal(User.StatusActive, user.Status);
    }

    [Theory]
    [InlineData(null, RoleName.STAFF)]
    [InlineData("", RoleName.STAFF)]
    [InlineData("staff", RoleName.STAFF)]
    [InlineData("ADMIN", RoleName.ADMIN)]
    public void ToRoleName_KnownOrMissingRole_ReturnsThatRoleOrStaffByDefault(string? raw,
        RoleName expected)
    {
        Assert.Equal(expected, RoleNameExtensions.ToRoleName(raw));
    }

    // ----------------------------------------------------------------- BCrypt

    [Fact]
    public void Hash_SamePasswordTwice_StartsWithDollarTwoAndUsesADifferentSaltEachTime()
    {
        var hashing = new BCryptPasswordHashingService();

        var first = hashing.Hash("SecurePass123");
        var second = hashing.Hash("SecurePass123");

        Assert.StartsWith("$2", first);
        Assert.NotEqual(first, second);
        Assert.True(hashing.Matches("SecurePass123", first));
        Assert.True(hashing.Matches("SecurePass123", second));
        Assert.False(hashing.Matches("WrongPass999", first));
    }

    [Theory]
    [InlineData("not-a-bcrypt-hash")]
    [InlineData("$2a$10$short")]
    public void Matches_MalformedHash_ReturnsFalseInsteadOfThrowing(string malformed)
    {
        var hashing = new BCryptPasswordHashingService();

        Assert.False(hashing.Matches("SecurePass123", malformed));
    }

    // -------------------------------------------------------------------- JWT

    [Fact]
    public void GenerateToken_AnyUser_CarriesSubjectEmailRoleAndConfiguredExpiration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:Jwt:Secret"] = Secret,
                ["Security:Jwt:ExpirationMinutes"] = "45"
            })
            .Build();
        var user = User.Register(new EmailAddress("owner@energix.test"), "hash", RoleName.ADMIN, false);

        var raw = new JwtTokenService(configuration).GenerateToken(user);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(raw);

        Assert.Equal(user.UserId.ToString(), token.Subject);
        Assert.Equal("owner@energix.test",
            token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("ADMIN", token.Claims.Single(c => c.Type is "role"
            or System.Security.Claims.ClaimTypes.Role).Value);
        Assert.InRange(token.ValidTo, DateTime.UtcNow.AddMinutes(44), DateTime.UtcNow.AddMinutes(46));
    }

    [Fact]
    public void GenerateToken_AnyUser_IsSignedWithTheConfiguredSecret()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Security:Jwt:Secret"] = Secret })
            .Build();
        var user = User.Register(new EmailAddress("owner@energix.test"), "hash", RoleName.STAFF, false);
        var raw = new JwtTokenService(configuration).GenerateToken(user);

        new JwtSecurityTokenHandler().ValidateToken(raw, new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret))
        }, out var validated);

        Assert.NotNull(validated);
    }
}
