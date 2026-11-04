using System.Net;
using Sems.Api.TestSupport;

namespace Sems.Api.IntegrationTests.Platform;

/// <summary>
/// Comprobaciones de salud y documentacion de la API, con la base disponible.
/// </summary>
public class HealthEndpointsTests : IClassFixture<SemsApiFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointsTests(SemsApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Health_WithoutToken_Returns200()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_DatabaseAvailable_Returns200()
    {
        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/auth/login")]
    [InlineData("/api/v1/organizations")]
    [InlineData("/api/v1/device-management/devices")]
    [InlineData("/api/v1/energy/bill-estimate")]
    [InlineData("/api/v1/analytics/bill-predictions/forecast")]
    [InlineData("/api/v1/demand-rules")]
    [InlineData("/api/v1/subscription-plans")]
    [InlineData("/api/v1/webhooks/stripe")]
    public async Task SwaggerJson_Anonymous_Returns200DocumentingTheEndpointsOfEveryModule(string path)
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        var document = await response.ExpectAsync(HttpStatusCode.OK);
        Assert.True(document.GetProperty("paths").TryGetProperty(path, out _), $"{path} no esta documentado");
    }
}

/// <summary>
/// Comprobaciones de salud con la base de datos inaccesible.
/// </summary>
public class HealthEndpointsWithoutDatabaseTests : IClassFixture<UnavailableDatabaseSemsApiFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointsWithoutDatabaseTests(UnavailableDatabaseSemsApiFactory factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task Health_DatabaseUnavailable_StillReturns200()
    {
        // /health responde a "el proceso esta vivo": no debe provocar reinicios
        // en cadena por un corte de la base de datos.
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_DatabaseUnavailable_Returns503()
    {
        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
