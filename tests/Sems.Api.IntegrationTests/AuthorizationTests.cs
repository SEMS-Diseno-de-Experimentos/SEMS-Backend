using System.Net;
using Sems.Api.TestSupport;

namespace Sems.Api.IntegrationTests;

/// <summary>
/// Que el dato de un usuario no se lea ni se escriba con el token de otro.
/// </summary>
/// <remarks>
/// <para>La politica de <c>Program.cs</c> ya exigia token en todas las rutas,
/// pero autenticar solo dice quien llama. Antes de estas pruebas la suite no
/// comprobaba de quien era el dato en ningun endpoint, y por eso un usuario
/// podia leer las lecturas, las alertas, los pagos y la suscripcion de
/// cualquier otro cambiando el identificador de la ruta.</para>
///
/// <para>Las pruebas van por HTTP a proposito: el agujero estaba en lo que
/// manda el cliente, y solo pasando por la capa que lo recibe se demuestra que
/// esta cerrado.</para>
/// </remarks>
public class AuthorizationTests : IClassFixture<SemsApiFactory>
{
    private readonly SemsApiFactory _factory;

    public AuthorizationTests(SemsApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ReadingAnotherUsersDataByItsRoute_IsForbidden()
    {
        var dueno = await _factory.CreateUserAsync();
        var ajeno = await _factory.CreateUserAsync();

        foreach (var ruta in RutasDe(dueno.UserId))
        {
            var respuesta = await ajeno.Client.GetAsync(ruta);
            Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        }
    }

    [Fact]
    public async Task ReadingOwnDataByItsRoute_IsAllowed()
    {
        var dueno = await _factory.CreateUserAsync();

        // La misma lista con el token correcto: la guardia no puede cerrarle la
        // puerta a quien si es el dueño, que seria romper la aplicacion entera.
        foreach (var ruta in RutasDe(dueno.UserId))
        {
            var respuesta = await dueno.Client.GetAsync(ruta);
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }
    }

    private static IEnumerable<string> RutasDe(Guid userId) => new[]
    {
        $"/api/v1/energy-meters/user/{userId}",
        $"/api/v1/energy-readings/user/{userId}",
        $"/api/v1/device-consumptions/user/{userId}",
        $"/api/v1/device-consumptions/user/{userId}/top",
        $"/api/v1/consumption-alerts/user/{userId}",
        $"/api/v1/consumption-alerts/user/{userId}/unread",
        $"/api/v1/users/{userId}/organizations",
        $"/api/v1/users/{userId}/alerts",
        $"/api/v1/users/{userId}/thresholds",
        $"/api/v1/users/{userId}/inactivity-rules",
        $"/api/v1/users/{userId}/notification-preferences",
        $"/api/v1/device-management/users/{userId}/devices",
        $"/api/v1/device-management/users/{userId}/bindings",
        $"/api/v1/analytics/bill-predictions/user/{userId}",
        $"/api/v1/analytics/recommendations/user/{userId}",
        $"/api/v1/analytics/anomalies/user/{userId}",
        $"/api/v1/analytics/device-identifications/user/{userId}",
        $"/api/v1/analytics/consumption-rankings/user/{userId}",
        $"/api/v1/subscriptions/users/{userId}",
        $"/api/v1/payments/user/{userId}",
        $"/api/v1/payment-methods/user/{userId}",
    };

    [Fact]
    public async Task ReadingAnotherUsersDataByTheQueryString_IsForbidden()
    {
        var dueno = await _factory.CreateUserAsync();
        var ajeno = await _factory.CreateUserAsync();

        // Este endpoint recibe el usuario en la cadena de consulta y no en la
        // ruta, que es lo que lo hacia facil de olvidar.
        var respuesta = await ajeno.Client.GetAsync(
            $"/api/v1/energy-readings/range?userId={dueno.UserId}"
            + "&from=2020-01-01T00:00:00Z&to=2030-01-01T00:00:00Z");
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task WritingDataInAnotherUsersName_IsForbidden()
    {
        var dueno = await _factory.CreateUserAsync();
        var ajeno = await _factory.CreateUserAsync();
        var siteId = await dueno.Client.CreateSiteForAsync(dueno.UserId);

        var lectura = await ajeno.Client.PostJsonAsync("/api/v1/energy-readings", new
        {
            user_id = dueno.UserId.ToString(),
            meter_id = Guid.NewGuid().ToString(),
            power_watts = 1200.0,
            frequency = 60.0,
            energy_kwh = 3.0
        });
        Assert.Equal(HttpStatusCode.Forbidden, lectura.StatusCode);

        var medidor = await ajeno.Client.PostMeterAsync(dueno.UserId,
            $"EOS-{Guid.NewGuid():N}", siteId);
        Assert.Equal(HttpStatusCode.Forbidden, medidor.StatusCode);

        var suscripcion = await ajeno.Client.PostJsonAsync("/api/v1/subscriptions", new
        {
            user_id = dueno.UserId.ToString(),
            plan_id = Guid.NewGuid().ToString()
        });
        Assert.Equal(HttpStatusCode.Forbidden, suscripcion.StatusCode);

        var organizacion = await ajeno.Client.PostOrganizationAsync(dueno.UserId,
            ApiRequests.NewTaxId(), "Comercial Ajena S.A.C.");
        Assert.Equal(HttpStatusCode.Forbidden, organizacion.StatusCode);
    }

    [Fact]
    public async Task TheRoutesWithoutAnIdentifier_ReturnOnlyTheCallersData()
    {
        var dueno = await _factory.CreateUserAsync();
        var ajeno = await _factory.CreateUserAsync();
        var siteId = await dueno.Client.CreateSiteForAsync(dueno.UserId);
        await dueno.Client.CreateDeviceAsync(dueno.UserId, siteId, null);

        // Estas dos rutas devolvian el inventario y las alertas de todos los
        // usuarios: no habia que adivinar ningun identificador para leer lo
        // ajeno, bastaba pedir la lista.
        var ajenos = await (await ajeno.Client.GetAsync("/api/v1/device-management/devices"))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.Empty(ajenos.EnumerateArray());

        var propios = await (await dueno.Client.GetAsync("/api/v1/device-management/devices"))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.Single(propios.EnumerateArray());

        var alertas = await (await ajeno.Client.GetAsync("/api/v1/alerts"))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.Empty(alertas.EnumerateArray());
    }

    [Fact]
    public async Task AMalformedUserIdentifier_IsABadRequestAndNotAForbidden()
    {
        var usuario = await _factory.CreateUserAsync();

        // El cliente se equivoco al escribirlo, no intento leer lo de otro, y
        // la respuesta tiene que distinguir las dos cosas.
        var respuesta = await usuario.Client.GetAsync("/api/v1/energy-meters/user/no-es-un-guid");
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }
}
