using Sems.Api.Modules.Iam.Domain.Model;

namespace Sems.Api.Modules.Iam.Domain.Services;

/// <summary>Hash de contrasenas. El dominio no sabe si detras hay BCrypt u otro.</summary>
public interface IPasswordHashingService
{
    string Hash(string plainPassword);

    bool Matches(string plainPassword, string passwordHash);
}

/// <summary>Emision del token de acceso.</summary>
public interface ITokenService
{
    string GenerateToken(User user);
}

/// <summary>
/// Puerto de salida de eventos del bounded context IAM.
///
/// <para>La capa de aplicacion no sabe como se entregan: eso permitio cambiar de
/// Kafka a entrega en proceso sin tocar los casos de uso.</para>
/// </summary>
public interface IIamEventPublisher
{
    void PublishUserRegistered(Guid userId, string emailAddress, string role);

    void PublishUserLoggedIn(Guid userId, string emailAddress);

    /// <summary>
    /// Pide el envio del codigo de verificacion. El token viaja en el evento
    /// porque es la unica vez que existe en claro: en base de datos solo queda
    /// su resumen.
    /// </summary>
    void PublishVerificationRequested(Guid userId, string emailAddress, string token);

    void PublishPasswordResetRequested(Guid userId, string emailAddress, string token);
}
