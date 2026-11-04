using Sems.Api.Modules.Energy.Domain.Model;
using Xunit;

namespace Sems.Api.Tests.Energy;

/// <summary>
/// Regla <see cref="HorarioPunta"/>: de 18:00 a 23:00, hora local de Peru
/// (UTC-5), todos los dias, salvo los domingos de un suministro con la
/// exclusion concedida.
/// </summary>
/// <remarks>
/// Los instantes se construyen en UTC a partir de la hora local, que es como
/// llegan las lecturas. Semana de referencia: del lunes 5 al domingo 11 de
/// octubre de 2026.
/// </remarks>
public class PeakHoursTests
{
    private static DateTime Utc(int dayOfOctober, int hour, int minute) =>
        new DateTime(2026, 10, dayOfOctober, hour, minute, 0, DateTimeKind.Utc).AddHours(5);

    [Theory]
    [InlineData(7, 17, 59, false, false)]   // miercoles 17:59
    [InlineData(7, 18, 0, false, true)]     // miercoles 18:00
    [InlineData(7, 22, 59, false, true)]    // miercoles 22:59
    [InlineData(7, 23, 0, false, false)]    // miercoles 23:00
    [InlineData(11, 20, 0, false, true)]    // domingo 20:00 sin exclusion
    [InlineData(11, 18, 0, false, true)]    // domingo 18:00 sin exclusion
    [InlineData(11, 18, 0, true, false)]    // domingo 18:00 con exclusion
    [InlineData(10, 21, 0, true, true)]     // sabado 21:00 con exclusion
    [InlineData(11, 11, 0, false, false)]   // domingo 11:00
    public void FranjaDe_LocalTimeInPeru_ClassifiesIntoTheExpectedBand(int dayOfOctober, int hour,
        int minute, bool excludesSundays, bool expectedPeak)
    {
        var band = HorarioPunta.FranjaDe(Utc(dayOfOctober, hour, minute), excludesSundays);

        Assert.Equal(expectedPeak ? FranjaHoraria.PUNTA : FranjaHoraria.FUERA_DE_PUNTA, band);
        Assert.Equal(expectedPeak, HorarioPunta.EsHoraPunta(Utc(dayOfOctober, hour, minute),
            excludesSundays));
    }

    [Theory]
    [InlineData(7, DayOfWeek.Wednesday)]
    [InlineData(10, DayOfWeek.Saturday)]
    [InlineData(11, DayOfWeek.Sunday)]
    public void AHoraLocal_ReferenceWeek_FallsOnTheExpectedDay(int dayOfOctober, DayOfWeek expected)
    {
        Assert.Equal(expected, HorarioPunta.AHoraLocal(Utc(dayOfOctober, 20, 0)).DayOfWeek);
    }

    [Fact]
    public void AHoraLocal_MidnightUtc_IsSevenPmOfThePreviousDayInPeru()
    {
        // 00:00 UTC del jueves son las 19:00 del miercoles en Lima: evaluar en UTC
        // daria fuera de punta, justo lo contrario de lo que es.
        var midnightUtc = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);

        var local = HorarioPunta.AHoraLocal(midnightUtc);

        Assert.Equal(new DateTime(2026, 10, 7, 19, 0, 0), local);
        Assert.Equal(FranjaHoraria.PUNTA, HorarioPunta.FranjaDe(midnightUtc));
    }
}
