namespace Sems.Api.Shared.Errors;

/// <summary>Categorias de error que reconoce la aplicacion.</summary>
public enum ErrorCode
{
    VALIDATION_ERROR,
    NOT_FOUND,
    CONFLICT,
    UNAUTHORIZED,
    INTERNAL_ERROR
}

/// <summary>
/// Error de aplicacion comun a todos los modulos.
///
/// <para>El dominio lanza esta excepcion y la capa HTTP la convierte en el mismo
/// JSON que ya consume el frontend: <c>{"code": "...", "message": "..."}</c>.
/// Mantener el contrato de error identico es lo que permite cambiar la
/// implementacion del backend sin tocar el manejo de errores del cliente.</para>
/// </summary>
public sealed class AppException : Exception
{
    public ErrorCode Code { get; }

    public AppException(ErrorCode code, string message) : base(message) => Code = code;

    public static AppException Validation(string message) => new(ErrorCode.VALIDATION_ERROR, message);

    public static AppException NotFound(string message) => new(ErrorCode.NOT_FOUND, message);

    public static AppException Conflict(string message) => new(ErrorCode.CONFLICT, message);

    public static AppException Unauthorized(string message) => new(ErrorCode.UNAUTHORIZED, message);

    public static AppException Internal(string message) => new(ErrorCode.INTERNAL_ERROR, message);
}

/// <summary>Cuerpo de error devuelto por la API.</summary>
public sealed record ErrorResponse(string Code, string Message);
