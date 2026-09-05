using System.Text.Json;
using Sems.Api.Shared.Errors;

namespace Sems.Api.Shared.Http;

/// <summary>
/// Traduce excepciones a respuestas HTTP con el contrato de error compartido.
///
/// <para>Cada categoria del dominio tiene un unico codigo de estado, igual que
/// en los servicios originales: validacion 400, no encontrado 404 y regla de
/// negocio rota 409.</para>
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            await WriteAsync(context, StatusFor(ex.Code), ex.Code.ToString(), ex.Message);
        }
        catch (FormatException)
        {
            // Un identificador mal formado en la ruta es culpa del cliente,
            // no un fallo del servidor.
            await WriteAsync(context, StatusCodes.Status400BadRequest,
                nameof(ErrorCode.VALIDATION_ERROR), "invalid identifier format");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado");
            await WriteAsync(context, StatusCodes.Status500InternalServerError,
                nameof(ErrorCode.INTERNAL_ERROR), "unexpected error");
        }
    }

    private static int StatusFor(ErrorCode code) => code switch
    {
        ErrorCode.VALIDATION_ERROR => StatusCodes.Status400BadRequest,
        ErrorCode.NOT_FOUND => StatusCodes.Status404NotFound,
        ErrorCode.CONFLICT => StatusCodes.Status409Conflict,
        ErrorCode.UNAUTHORIZED => StatusCodes.Status401Unauthorized,
        _ => StatusCodes.Status500InternalServerError
    };

    private static async Task WriteAsync(HttpContext context, int status, string code, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }
        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(new ErrorResponse(code, message), JsonOptions));
    }
}
