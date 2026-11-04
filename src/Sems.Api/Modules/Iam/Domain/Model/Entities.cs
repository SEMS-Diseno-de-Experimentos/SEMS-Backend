using System.Text.RegularExpressions;
using Sems.Api.Shared.Errors;

namespace Sems.Api.Modules.Iam.Domain.Model;

/// <summary>
/// Correo electronico validado.
///
/// <para>Envolverlo en un tipo propio impide que una cadena cualquiera llegue
/// donde se espera un correo, y centraliza la validacion en un solo sitio.</para>
/// </summary>
public sealed record EmailAddress
{
    private static readonly Regex Pattern =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public string Value { get; }

    public EmailAddress(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!Pattern.IsMatch(normalized))
        {
            throw AppException.Validation("email address is invalid");
        }
        Value = normalized;
    }

    public override string ToString() => Value;
}

/// <summary>
/// Rol de la persona en la plataforma.
/// </summary>
/// <remarks>
/// <para>Es distinto del papel que tiene dentro de una organizacion, que vive
/// en el modulo de organizaciones y decide que locales puede ver. Aqui solo se
/// distingue a quien usa el producto de quien lo administra.</para>
///
/// <para>Antes el rol por defecto se llamaba RESIDENT, del segmento
/// residencial. Ya no describe a nadie: quien entra ahora es el personal de un
/// establecimiento.</para>
/// </remarks>
public enum RoleName
{
    /// <summary>Personal de un establecimiento. Es el rol por defecto.</summary>
    STAFF,

    /// <summary>Administrador de la plataforma.</summary>
    ADMIN
}

public static class RoleNameExtensions
{
    public static RoleName ToRoleName(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? RoleName.STAFF
            : Enum.TryParse<RoleName>(value.Trim(), ignoreCase: true, out var r) && Enum.IsDefined(r)
                ? r
                : throw AppException.Validation("role is invalid");
}

/// <summary>
/// Segmento comercial del usuario, los dos a los que apunta el producto.
/// </summary>
/// <remarks>
/// Es un dato del perfil, no un permiso: no decide lo que se puede hacer, solo
/// con que cara se presenta la aplicacion.
/// </remarks>
public enum UserSegment
{
    /// <summary>Cadena con varios establecimientos.</summary>
    RETAIL_CHAIN,

    /// <summary>Establecimiento independiente.</summary>
    INDEPENDENT
}

public static class UserSegmentExtensions
{
    /// <summary>
    /// Acepta el valor tal como lo escribe el cliente, sin distinguir
    /// mayusculas ni espacios.
    /// </summary>
    /// <remarks>
    /// Devuelve null cuando no viene nada, porque el segmento es opcional: el
    /// alta funciona igual sin declararlo.
    /// </remarks>
    public static UserSegment? ToUserSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var normalizado = value.Trim().ToUpperInvariant().Replace('-', '_');
        return Enum.TryParse<UserSegment>(normalizado, out var segmento)
               && Enum.IsDefined(segmento)
            ? segmento
            : throw AppException.Validation("segment must be one of RETAIL_CHAIN, INDEPENDENT");
    }
}

/// <summary>
/// Usuario del sistema.
///
/// <para>La contrasena solo existe aqui como resumen: el valor en claro nunca se
/// almacena ni se puede recuperar.</para>
/// </summary>
public class User
{
    public const string StatusPending = "PENDING";
    public const string StatusActive = "ACTIVE";

    public Guid UserId { get; private set; }
    public string EmailAddress { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public RoleName Role { get; private set; }

    /// <summary>
    /// PENDING mientras no se verifica el correo, ACTIVE despues. Es anulable
    /// para no romper las filas anteriores a la verificacion; un valor ausente
    /// se trata como ACTIVE.
    /// </summary>
    public string? Status { get; private set; }

    /// <summary>
    /// Nombre con el que la persona quiere que se la llame. Opcional.
    /// </summary>
    /// <remarks>
    /// Lo guarda el backend y no el navegador: antes el nombre que el usuario
    /// escribia al registrarse solo vivia en ese equipo, asi que entrar desde
    /// otro lo perdia y el saludo salia con la parte local del correo.
    /// </remarks>
    public string? FullName { get; private set; }

    /// <summary>Segmento comercial declarado. Opcional.</summary>
    public UserSegment? Segment { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private User()
    {
    }

    public static User Register(EmailAddress email, string passwordHash, RoleName role,
        bool requireVerification, string? fullName = null, UserSegment? segment = null)
    {
        var now = DateTime.UtcNow;
        return new User
        {
            UserId = Guid.NewGuid(),
            EmailAddress = email.Value,
            PasswordHash = passwordHash,
            Role = role,
            FullName = Limpio(fullName),
            Segment = segment,
            Status = requireVerification ? StatusPending : StatusActive,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Cambia el nombre y el segmento. Ambos pueden quedar vacios.</summary>
    public void UpdateProfile(string? fullName, UserSegment? segment)
    {
        FullName = Limpio(fullName);
        Segment = segment;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Un nombre en blanco es lo mismo que no tener nombre.</summary>
    private static string? Limpio(string? valor)
    {
        var nombre = valor?.Trim();
        if (string.IsNullOrEmpty(nombre))
        {
            return null;
        }
        if (nombre.Length > 120)
        {
            throw AppException.Validation("full_name must be at most 120 characters");
        }
        return nombre;
    }

    public bool IsPending => string.Equals(Status, StatusPending, StringComparison.OrdinalIgnoreCase);

    public void Activate()
    {
        Status = StatusActive;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Token de refresco emitido a un usuario.
///
/// <para><b>Se guarda el resumen SHA-256, nunca el token en claro.</b> Si alguien
/// obtuviera una copia de esta tabla no podria suplantar a nadie: el valor
/// almacenado no sirve para autenticarse.</para>
/// </summary>
public class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public bool Revoked { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private RefreshToken()
    {
    }

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTime expiresAt) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TokenHash = tokenHash,
        ExpiresAt = expiresAt,
        Revoked = false,
        CreatedAt = DateTime.UtcNow
    };

    public void Revoke() => Revoked = true;

    public bool IsUsable(DateTime now) => !Revoked && ExpiresAt > now;
}

/// <summary>
/// Token de un solo uso: verificacion de cuenta o recuperacion de contrasena.
///
/// <para>Como los de refresco, se guarda el resumen y no el valor en claro. La
/// marca de uso garantiza que un enlace no pueda reutilizarse aunque el correo
/// quede archivado o se reenvie.</para>
/// </summary>
public class UserAuthToken
{
    public const string PurposeVerification = "VERIFICATION";
    public const string PurposePasswordReset = "PASSWORD_RESET";

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public string Purpose { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private UserAuthToken()
    {
    }

    public static UserAuthToken Issue(Guid userId, string tokenHash, string purpose,
        DateTime expiresAt) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TokenHash = tokenHash,
        Purpose = purpose,
        ExpiresAt = expiresAt,
        CreatedAt = DateTime.UtcNow
    };

    public void MarkUsed() => UsedAt = DateTime.UtcNow;

    public bool IsUsable(DateTime now) => UsedAt is null && ExpiresAt > now;
}
