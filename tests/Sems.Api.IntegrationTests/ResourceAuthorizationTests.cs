using System.Net;
using System.Text.Json;
using Sems.Api.TestSupport;

namespace Sems.Api.IntegrationTests;

/// <summary>
/// Que un recurso con identificador propio no se lea ni se escriba desde otra
/// cuenta, y que si lo alcance quien supervisa su local.
/// </summary>
/// <remarks>
/// <para>Las rutas de esta clase no llevan el usuario en ninguna parte: llevan
/// el identificador del medidor, del equipo, del local o de la organizacion.
/// Comprobar el token contra la ruta no sirve de nada aqui, porque en la ruta
/// no hay nada que comparar; hay que cargar el recurso y preguntar de quien
/// es.</para>
///
/// <para>La segunda mitad de la clase es la que importa de verdad: cerrar por
/// dueño es facil, y dejaria al jefe de tienda sin ver su propio local. Esas
/// pruebas son las que demuestran que no paso eso.</para>
/// </remarks>
public class ResourceAuthorizationTests : IClassFixture<SemsApiFactory>
{
    private readonly SemsApiFactory _factory;

    public ResourceAuthorizationTests(SemsApiFactory factory) => _factory = factory;

    /// <summary>Un local con un medidor, un equipo y una lectura dentro.</summary>
    private sealed record Montaje(TestUser Dueno, Guid OrganizationId, Guid SiteId, Guid ZoneId,
        string MeterId, Guid DeviceId);

    private async Task<Montaje> MontarAsync()
    {
        var dueno = await _factory.CreateUserAsync();
        var organizacion = await dueno.Client.CreateOrganizationAsync(dueno.UserId);
        var organizationId = Guid.Parse(organizacion.Text("organization_id"));
        var local = await dueno.Client.CreateSiteAsync(organizationId,
            $"S-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}");
        var siteId = Guid.Parse(local.Text("site_id"));
        var zona = await dueno.Client.CreateZoneAsync(siteId, "Camara de frio");
        var zoneId = Guid.Parse(zona.Text("zone_id"));

        var medidor = await (await dueno.Client.PostMeterAsync(dueno.UserId,
            $"EOS-{Guid.NewGuid():N}", siteId, zoneId)).ExpectAsync(HttpStatusCode.Created);
        var equipo = await dueno.Client.CreateDeviceAsync(dueno.UserId, siteId, zoneId);

        await (await dueno.Client.PostReadingAsync(dueno.UserId, medidor.Text("id"), null,
            powerWatts: 2400, energyKwh: 6.0)).ExpectAsync(HttpStatusCode.Created);

        return new Montaje(dueno, organizationId, siteId, zoneId, medidor.Text("id"),
            Guid.Parse(equipo.Text("deviceId")));
    }

    [Fact]
    public async Task ReadingAnotherAccountsResourcesByTheirIdentifier_IsForbidden()
    {
        var montaje = await MontarAsync();
        var ajeno = await _factory.CreateUserAsync();

        foreach (var ruta in new[]
                 {
                     $"/api/v1/energy-meters/{montaje.MeterId}",
                     $"/api/v1/energy-meters/site/{montaje.SiteId}",
                     $"/api/v1/energy-meters/zone/{montaje.ZoneId}",
                     $"/api/v1/energy-readings/meter/{montaje.MeterId}/latest",
                     $"/api/v1/sites/{montaje.SiteId}/zone-consumption",
                     $"/api/v1/sites/{montaje.SiteId}/consumption/current",
                     $"/api/v1/organizations/{montaje.OrganizationId}/site-consumption",
                     $"/api/v1/organizations/{montaje.OrganizationId}",
                     $"/api/v1/organizations/{montaje.OrganizationId}/sites",
                     $"/api/v1/organizations/{montaje.OrganizationId}/members",
                     $"/api/v1/sites/{montaje.SiteId}",
                     $"/api/v1/sites/{montaje.SiteId}/zones",
                     $"/api/v1/device-management/devices/{montaje.DeviceId}",
                     $"/api/v1/device-management/devices/{montaje.DeviceId}/bindings",
                     $"/api/v1/device-management/devices/{montaje.DeviceId}/configurations",
                     $"/api/v1/device-management/devices/{montaje.DeviceId}/events",
                     $"/api/v1/device-management/sites/{montaje.SiteId}/devices",
                     $"/api/v1/device-management/zones/{montaje.ZoneId}/devices",
                     $"/api/v1/sites/{montaje.SiteId}/demand-rules",
                 })
        {
            var respuesta = await ajeno.Client.GetAsync(ruta);
            Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        }
    }

    [Fact]
    public async Task WritingToAnotherAccountsResources_IsForbidden()
    {
        var montaje = await MontarAsync();
        var ajeno = await _factory.CreateUserAsync();

        // Esta era la escritura mas dañina que quedaba abierta: cortar la
        // medicion del suministro de otro con una sola peticion.
        var baja = await ajeno.Client.PatchAsync(
            $"/api/v1/energy-meters/{montaje.MeterId}/deactivate", null);
        Assert.Equal(HttpStatusCode.Forbidden, baja.StatusCode);

        var equipo = await ajeno.Client.DeleteAsync(
            $"/api/v1/device-management/devices/{montaje.DeviceId}");
        Assert.Equal(HttpStatusCode.Forbidden, equipo.StatusCode);

        var estado = await ajeno.Client.PatchJsonAsync(
            $"/api/v1/device-management/devices/{montaje.DeviceId}/status",
            new { status = "INACTIVE" });
        Assert.Equal(HttpStatusCode.Forbidden, estado.StatusCode);

        var local = await ajeno.Client.DeleteAsync($"/api/v1/sites/{montaje.SiteId}");
        Assert.Equal(HttpStatusCode.Forbidden, local.StatusCode);

        var zona = await ajeno.Client.DeleteAsync($"/api/v1/zones/{montaje.ZoneId}");
        Assert.Equal(HttpStatusCode.Forbidden, zona.StatusCode);

        var renombrar = await ajeno.Client.PutJsonAsync(
            $"/api/v1/organizations/{montaje.OrganizationId}",
            new { legal_name = "Mia Ahora S.A.C.", business_type = "SUPERMARKET" });
        Assert.Equal(HttpStatusCode.Forbidden, renombrar.StatusCode);

        var invitar = await ajeno.Client.PostJsonAsync(
            $"/api/v1/organizations/{montaje.OrganizationId}/members",
            new { user_id = ajeno.UserId.ToString(), role = "ORG_ADMIN" });
        Assert.Equal(HttpStatusCode.Forbidden, invitar.StatusCode);
    }

    [Fact]
    public async Task TheOwnerOfTheSite_StillReachesEverythingInIt()
    {
        var montaje = await MontarAsync();

        // La otra mitad de la prueba anterior: cerrar de mas rompe el producto.
        foreach (var ruta in new[]
                 {
                     $"/api/v1/energy-meters/{montaje.MeterId}",
                     $"/api/v1/energy-meters/site/{montaje.SiteId}",
                     $"/api/v1/sites/{montaje.SiteId}/zone-consumption",
                     $"/api/v1/organizations/{montaje.OrganizationId}/members",
                     $"/api/v1/sites/{montaje.SiteId}/zones",
                     $"/api/v1/device-management/devices/{montaje.DeviceId}",
                     $"/api/v1/device-management/sites/{montaje.SiteId}/devices",
                 })
        {
            var respuesta = await montaje.Dueno.Client.GetAsync(ruta);
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }
    }

    [Fact]
    public async Task ASupervisorOfTheSite_ReachesItsDataWithoutOwningAnythingInIt()
    {
        var montaje = await MontarAsync();
        var supervisor = await _factory.CreateUserAsync();

        // Ni el medidor ni el equipo son suyos: los registro el dueño. Lo que
        // le da acceso es la membresia, que es de lo que vive este modelo.
        await (await montaje.Dueno.Client.PostJsonAsync(
            $"/api/v1/organizations/{montaje.OrganizationId}/members",
            new
            {
                user_id = supervisor.UserId.ToString(),
                role = "SUPERVISOR",
                site_id = montaje.SiteId.ToString()
            })).ExpectAsync(HttpStatusCode.Created);

        var medidores = await (await supervisor.Client.GetAsync(
            $"/api/v1/energy-meters/site/{montaje.SiteId}")).ExpectAsync(HttpStatusCode.OK);
        Assert.Single(medidores.EnumerateArray());

        var equipos = await (await supervisor.Client.GetAsync(
            $"/api/v1/device-management/sites/{montaje.SiteId}/devices"))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.Single(equipos.EnumerateArray());

        foreach (var ruta in new[]
                 {
                     $"/api/v1/energy-meters/{montaje.MeterId}",
                     $"/api/v1/device-management/devices/{montaje.DeviceId}",
                     $"/api/v1/sites/{montaje.SiteId}/zone-consumption",
                     $"/api/v1/sites/{montaje.SiteId}/consumption/current",
                     $"/api/v1/sites/{montaje.SiteId}/zones",
                 })
        {
            var respuesta = await supervisor.Client.GetAsync(ruta);
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }
    }

    [Fact]
    public async Task ASupervisorOfTheSite_StillCannotActOnTheChainsContract()
    {
        var montaje = await MontarAsync();
        var supervisor = await _factory.CreateUserAsync();
        await (await montaje.Dueno.Client.PostJsonAsync(
            $"/api/v1/organizations/{montaje.OrganizationId}/members",
            new
            {
                user_id = supervisor.UserId.ToString(),
                role = "SUPERVISOR",
                site_id = montaje.SiteId.ToString()
            })).ExpectAsync(HttpStatusCode.Created);

        // Ve su local, pero la potencia contratada y los accesos de la cadena
        // no son cosa suya: para eso hace falta administrar la organizacion.
        var archivar = await supervisor.Client.DeleteAsync(
            $"/api/v1/sites/{montaje.SiteId}");
        Assert.Equal(HttpStatusCode.Forbidden, archivar.StatusCode);

        var invitar = await supervisor.Client.PostJsonAsync(
            $"/api/v1/organizations/{montaje.OrganizationId}/members",
            new { user_id = supervisor.UserId.ToString(), role = "OPERATOR",
                  site_id = montaje.SiteId.ToString() });
        Assert.Equal(HttpStatusCode.Forbidden, invitar.StatusCode);
    }

    [Fact]
    public async Task AnAdminOfOneChain_CannotRevokeAMembershipOfAnother()
    {
        var unaCadena = await MontarAsync();
        var otraCadena = await MontarAsync();

        var vinculos = await (await otraCadena.Dueno.Client.GetAsync(
            $"/api/v1/organizations/{otraCadena.OrganizationId}/members"))
            .ExpectAsync(HttpStatusCode.OK);
        var ajeno = vinculos.EnumerateArray().First().Text("membership_id");

        // Administrar una organizacion no da derecho sobre los vinculos de
        // otra, aunque la ruta lleve la propia: el vinculo tiene que ser de
        // la organizacion que dice la ruta.
        var respuesta = await unaCadena.Dueno.Client.DeleteAsync(
            $"/api/v1/organizations/{unaCadena.OrganizationId}/members/{ajeno}");
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }
}
