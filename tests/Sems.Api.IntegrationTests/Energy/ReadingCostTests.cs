using System.Net;
using System.Text.Json;
using Sems.Api.TestSupport;

namespace Sems.Api.IntegrationTests.Energy;

/// <summary>
/// El importe de cada lectura, por HTTP y en snake_case.
/// </summary>
/// <remarks>
/// La aplicacion web suma estos importes para enseñar el gasto del dia, del
/// periodo y por equipo. Antes el campo no existia en la respuesta y la web lo
/// leia igual: sumaba cero y pintaba S/ 0.00 sobre cientos de kWh medidos.
/// </remarks>
public class ReadingCostTests : IClassFixture<SemsApiFactory>
{
    private readonly SemsApiFactory _factory;

    public ReadingCostTests(SemsApiFactory factory) => _factory = factory;

    /// <summary>
    /// Ocho kWh a proposito: con la tarifa MT2 dan 1.916 y 2.248, dos numeros
    /// que no caen en el medio punto y redondean sin ambiguedad.
    /// </summary>
    private const double Kwh = 8.0;

    /// <summary>Las 19:30 de Lima, que es hora punta, son las 00:30 UTC del dia siguiente.</summary>
    private static DateTime EnPunta => DateTime.UtcNow.Date.AddDays(-2).AddHours(24.5);

    /// <summary>Las 10:00 de Lima, fuera de punta, son las 15:00 UTC.</summary>
    private static DateTime FueraDePunta => DateTime.UtcNow.Date.AddDays(-2).AddHours(15);

    private static JsonElement Con(JsonElement lecturas, DateTime instante) =>
        lecturas.EnumerateArray().Single(l =>
            DateTime.Parse(l.Text("timestamp"), null,
                System.Globalization.DateTimeStyles.AdjustToUniversal
                | System.Globalization.DateTimeStyles.AssumeUniversal) == instante);

    [Fact]
    public async Task ReadingsOfAUser_AreValuedByTheTimeBandOfEachReading()
    {
        var user = await _factory.CreateUserAsync();
        var client = user.Client;
        var siteId = await client.CreateSiteForAsync(user.UserId, tariffCategory: "MT2");
        var meterId = (await (await client.PostMeterAsync(user.UserId, $"EOS-{Guid.NewGuid():N}",
            siteId)).ExpectAsync(HttpStatusCode.Created)).Text("id");

        foreach (var instante in new[] { EnPunta, FueraDePunta })
        {
            await (await client.PostReadingAsync(user.UserId, meterId, null, powerWatts: 0,
                energyKwh: Kwh, timestamp: instante)).ExpectAsync(HttpStatusCode.Created);
        }

        var lecturas = await (await client.GetAsync(
            $"/api/v1/energy-readings/user/{user.UserId}")).ExpectAsync(HttpStatusCode.OK);

        // 8 kWh x 0.2810 el kWh de punta.
        Assert.Equal(2.25m, Con(lecturas, EnPunta).Number("estimated_cost"));
        // 8 kWh x 0.2395 el kWh fuera de punta: la misma energia cuesta menos.
        Assert.Equal(1.92m, Con(lecturas, FueraDePunta).Number("estimated_cost"));
    }

    [Fact]
    public async Task ReadingsOfAnUnregisteredMeter_CarryNoCostInsteadOfZero()
    {
        var user = await _factory.CreateUserAsync();
        var client = user.Client;

        // El alta de la lectura no exige que el medidor este registrado, asi que
        // llegan lecturas de medidores que la plataforma no conoce. Sin medidor
        // no hay local, sin local no hay tarifa y sin tarifa no hay importe.
        await (await client.PostReadingAsync(user.UserId, Guid.NewGuid().ToString(), null,
            powerWatts: 0, energyKwh: Kwh, timestamp: FueraDePunta))
            .ExpectAsync(HttpStatusCode.Created);

        var lecturas = await (await client.GetAsync(
            $"/api/v1/energy-readings/user/{user.UserId}")).ExpectAsync(HttpStatusCode.OK);

        // La ausencia del campo es la respuesta honesta. Un cero seria un importe,
        // y la pantalla lo sumaria como si esa energia hubiera salido gratis.
        var lectura = lecturas.EnumerateArray().Single();
        Assert.False(lectura.TryGetProperty("estimated_cost", out _));
    }
}
