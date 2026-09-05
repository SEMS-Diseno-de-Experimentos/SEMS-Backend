using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Sems.Api.Modules.Iam.Domain.Model;
using Sems.Api.Modules.Iam.Domain.Services;
using Sems.Api.Shared.Events;

namespace Sems.Api.Modules.Iam.Infrastructure;

/// <summary>Hash de contrasenas con BCrypt.</summary>
public sealed class BCryptPasswordHashingService : IPasswordHashingService
{
    public string Hash(string plainPassword) => BCrypt.Net.BCrypt.HashPassword(plainPassword);

    public bool Matches(string plainPassword, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(plainPassword, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Un hash con formato invalido no debe reventar el inicio de sesion:
            // simplemente no coincide.
            return false;
        }
    }
}

/// <summary>Emision del token de acceso JWT.</summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly string _secret;
    private readonly int _expirationMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        _secret = configuration["Security:Jwt:Secret"]
                  ?? Environment.GetEnvironmentVariable("JWT_SECRET")
                  ?? "replace_with_minimum_32_chars_secret_key_for_hs256";
        _expirationMinutes = int.TryParse(configuration["Security:Jwt:ExpirationMinutes"]
                                          ?? Environment.GetEnvironmentVariable("JWT_EXPIRATION_MINUTES"),
            out var m) ? m : 120;
    }

    public string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.EmailAddress),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>
/// Adaptador de salida del bounded context IAM.
///
/// <para>Sustituye al productor de Kafka. El puerto no cambia: la capa de
/// aplicacion sigue sin saber como se entregan los eventos.</para>
/// </summary>
public sealed class InProcessIamEventPublisher : IIamEventPublisher
{
    private readonly IDomainEventBus _bus;

    public InProcessIamEventPublisher(IDomainEventBus bus) => _bus = bus;

    public void PublishUserRegistered(Guid userId, string emailAddress, string role) =>
        _bus.Publish(new DomainEvents.UserRegistered(userId, emailAddress, role));

    public void PublishUserLoggedIn(Guid userId, string emailAddress) =>
        _bus.Publish(new DomainEvents.UserLoggedIn(userId, emailAddress));

    public void PublishVerificationRequested(Guid userId, string emailAddress, string token) =>
        _bus.Publish(new DomainEvents.VerificationRequested(userId, emailAddress, token));

    public void PublishPasswordResetRequested(Guid userId, string emailAddress, string token) =>
        _bus.Publish(new DomainEvents.PasswordResetRequested(userId, emailAddress, token));
}
