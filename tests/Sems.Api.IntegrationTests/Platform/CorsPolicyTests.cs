using System.Net;
using Sems.Api.TestSupport;

namespace Sems.Api.IntegrationTests.Platform;

/// <summary>
/// Politica CORS: solo las aplicaciones registradas pueden llamar a la API
/// desde el navegador con credenciales (TS09).
/// </summary>
public class CorsPolicyTests : IClassFixture<SemsApiFactory>
{
    private const string AllowOrigin = "Access-Control-Allow-Origin";

    private readonly HttpClient _client;

    public CorsPolicyTests(SemsApiFactory factory) => _client = factory.CreateClient();

    private Task<HttpResponseMessage> PreflightAsync(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,authorization");
        return _client.SendAsync(request);
    }

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("https://sems-web-application.vercel.app")]
    [InlineData("https://sems-diseno-web.vercel.app")]
    public async Task Preflight_RegisteredOrigin_IncludesAllowOriginHeader(string origin)
    {
        var response = await PreflightAsync(origin);

        Assert.True(response.Headers.TryGetValues(AllowOrigin, out var values));
        Assert.Equal(origin, Assert.Single(values!));
    }

    [Theory]
    [InlineData("https://attacker.example.com")]
    [InlineData("http://localhost:3000")]
    public async Task Preflight_UnregisteredOrigin_OmitsAllowOriginHeader(string origin)
    {
        var response = await PreflightAsync(origin);

        Assert.False(response.Headers.Contains(AllowOrigin),
            $"{origin} no esta registrado y aun asi recibio {AllowOrigin}");
    }
}
