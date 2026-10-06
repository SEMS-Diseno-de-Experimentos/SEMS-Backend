using System.Globalization;
using System.Net;
using Sems.Api.Modules.Energy.Domain.Model;
using Sems.Api.TestSupport;
using TechTalk.SpecFlow;

namespace Sems.Api.Specs.Steps;

/// <summary>
/// Horario de punta de la tarifa comercial (EP05, US27).
/// </summary>
/// <remarks>
/// Ningun endpoint clasifica una lectura suelta por franja, asi que la
/// clasificacion ejecuta la regla de dominio <see cref="HorarioPunta"/>; la
/// tarifa publicada se comprueba a traves de la API.
/// </remarks>
[Binding]
public sealed class PeakHoursSteps
{
    /// <summary>Semana de referencia: del lunes 5 al domingo 11 de octubre de 2026.</summary>
    private static readonly DateTime ReferenceMonday = new(2026, 10, 5);

    private readonly ApiWorld _world;
    private bool _excludesSundays;
    private FranjaHoraria _band;

    public PeakHoursSteps(ApiWorld world) => _world = world;

    [Given(@"a site whose supply (has|does not have) the Sunday exclusion")]
    public void GivenASiteWhoseSupplyHasTheSundayExclusion(string answer) =>
        _excludesSundays = answer == "has";

    [When(@"a reading arrives on (Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday) at (\d{2}:\d{2}) local time")]
    public void WhenAReadingArrivesOnAtLocalTime(string day, string time)
    {
        var dayOfWeek = Enum.Parse<DayOfWeek>(day);
        var offset = ((int)dayOfWeek + 6) % 7;   // lunes = 0
        var localTime = TimeSpan.ParseExact(time, @"hh\:mm", CultureInfo.InvariantCulture);
        var local = ReferenceMonday.AddDays(offset).Add(localTime);

        // Las lecturas llegan en UTC; Peru esta en UTC-5.
        var utc = DateTime.SpecifyKind(local.AddHours(5), DateTimeKind.Utc);
        Assert.Equal(dayOfWeek, HorarioPunta.AHoraLocal(utc).DayOfWeek);

        _band = HorarioPunta.FranjaDe(utc, _excludesSundays);
    }

    [Then(@"the reading falls in the (peak|off-peak) band")]
    public void ThenTheReadingFallsInTheBand(string band) =>
        Assert.Equal(band == "peak" ? FranjaHoraria.PUNTA : FranjaHoraria.FUERA_DE_PUNTA, _band);

    [When(@"the user consults the ""(.*)"" tariff")]
    public Task WhenTheUserConsultsTheTariff(string category) =>
        _world.RecordAsync(_world.SignedInUser.Client.GetAsync($"/api/v1/energy/tariffs/{category}"));

    [Then(@"the peak hours are ""(.*)""")]
    public void ThenThePeakHoursAre(string peakHours)
    {
        Assert.Equal(HttpStatusCode.OK, _world.Response.StatusCode);
        Assert.Equal(peakHours, _world.LastBody.Text("peak_hours"));
    }
}
