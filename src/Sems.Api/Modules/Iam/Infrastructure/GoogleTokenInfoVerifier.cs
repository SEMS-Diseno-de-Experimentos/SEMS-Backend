using System.Text.Json;
using Sems.Api.Modules.Iam.Domain.Services;

namespace Sems.Api.Modules.Iam.Infrastructure;

/// <summary>
/// Verifica el token preguntandole a Google por el.
/// </summary>
/// <remarks>
/// <para>Se usa <c>tokeninfo</c> en vez de comprobar la firma aqui con las
/// claves publicas de Google. Es una llamada de red por inicio de sesion, pero
/// a cambio la rotacion de claves, los algoritmos y la caducidad los lleva
/// Google: criptografia propia en el camino de autenticacion es justo donde un
/// error no se nota hasta que alguien entra sin permiso.</para>
/// </remarks>
public sealed class GoogleTokenInfoVerifier : IGoogleTokenVerifier
{
    private readonly HttpClient _http;
    private readonly ILogger<GoogleTokenInfoVerifier> _logger;

    public GoogleTokenInfoVerifier(HttpClient http, ILogger<GoogleTokenInfoVerifier> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IGoogleTokenVerifier.GoogleIdentity?> VerifyAsync(string idToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        try
        {
            using var respuesta = await _http.GetAsync(
                $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}",
                ct);

            // Un token caducado o manipulado sale por aqui: Google responde 400.
            if (!respuesta.IsSuccessStatusCode)
            {
                return null;
            }

            using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
            var raiz = cuerpo.RootElement;

            var correo = Texto(raiz, "email");
            return string.IsNullOrWhiteSpace(correo)
                ? null
                : new IGoogleTokenVerifier.GoogleIdentity(
                    Texto(raiz, "aud") ?? string.Empty,
                    correo,
                    // Google lo manda como la cadena "true", no como booleano.
                    string.Equals(Texto(raiz, "email_verified"), "true",
                        StringComparison.OrdinalIgnoreCase),
                    Texto(raiz, "name"));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                       or JsonException)
        {
            // Sin respuesta de Google no se puede afirmar nada del token. Se
            // trata como no valido, que es lo seguro, y se registra para poder
            // distinguir "token falso" de "Google no contesta".
            _logger.LogWarning(ex, "No se pudo verificar el token de Google");
            return null;
        }
    }

    private static string? Texto(JsonElement raiz, string campo) =>
        raiz.TryGetProperty(campo, out var valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;
}
