using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sems.Api.Modules.Analytics.Application;
using Sems.Api.Modules.Analytics.Domain.Model;
using Sems.Api.Modules.Analytics.Domain.Repositories;
using Sems.Api.Modules.Analytics.Domain.Services;
using Xunit;

namespace Sems.Api.Tests.Analytics;

/// <summary>
/// El productor de hallazgos, con el consumo medido simulado.
/// </summary>
/// <remarks>
/// <para>Lo que se comprueba aqui no es la aritmetica sino el criterio: que una
/// anomalia solo se afirme con historia suficiente y con un desvio que se note,
/// y que cada recomendacion salga del numero que la justifica. Un hallazgo que
/// no se sostiene se ignora, y una lista de hallazgos ignorados no sirve de
/// nada.</para>
///
/// <para>Las fechas se cuentan desde ayer hacia atras porque el motor solo mira
/// dias cerrados: el dia en curso esta a medias.</para>
/// </remarks>
public class InsightEngineTests
{
    private readonly IAnomalyRepository _anomalies = Substitute.For<IAnomalyRepository>();
    private readonly IRecommendationRepository _recommendations =
        Substitute.For<IRecommendationRepository>();
    private readonly IBillPredictionRepository _predictions =
        Substitute.For<IBillPredictionRepository>();
    private readonly IBillCalculator _calculator = Substitute.For<IBillCalculator>();
    private readonly IEnergyUsage _energia = Substitute.For<IEnergyUsage>();
    private readonly InsightEngine _motor;

    private static readonly Guid Local = Guid.NewGuid();
    private static readonly Guid Medidor = Guid.NewGuid();
    private static readonly Guid Zona = Guid.NewGuid();
    private const string Usuario = "11111111-1111-1111-1111-111111111111";

    public InsightEngineTests()
    {
        _anomalies.SaveAsync(Arg.Any<Anomaly>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Anomaly>());
        _recommendations.SaveAsync(Arg.Any<Recommendation>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Recommendation>());
        _predictions.SaveAsync(Arg.Any<BillPrediction>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<BillPrediction>());
        _anomalies.FindByUserIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<Anomaly>());
        _recommendations.FindByUserIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<Recommendation>());
        _predictions.FindByUserIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<BillPrediction>());
        _calculator.Estimate(Arg.Any<string?>(), Arg.Any<double>(), Arg.Any<double>(),
                Arg.Any<double>(), Arg.Any<double>())
            .Returns(new EstimatedBill(1000, 7000, 12.80, 9452.70, "PEN", 0.2395));
        _energia.PricesOfAsync(Local, Arg.Any<CancellationToken>()).Returns(Precios());
        _energia.ZonesOfAsync(Local, Arg.Any<CancellationToken>())
            .Returns(new List<IEnergyUsage.ZoneName> { new(Zona, "Camaras de frio") });

        var analytics = new AnalyticsService(_predictions, _recommendations, _anomalies,
            Substitute.For<IDeviceIdentificationRepository>(),
            Substitute.For<IConsumptionRankingRepository>(), _calculator);

        _motor = new InsightEngine(_anomalies, _recommendations, analytics, _energia,
            NullLogger<InsightEngine>.Instance);
    }

    /// <summary>Un local de 120 kW contratados en tarifa MT2.</summary>
    private static IEnergyUsage.SitePrices Precios() =>
        new("Sede Miraflores", "MT2", 120m, "PEN", 0.2810m, 0.2395m, 58.40m);

    private static DateOnly Dia(int haceDias) =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5).AddDays(-haceDias));

    /// <summary>
    /// Dias de consumo plano, del mas antiguo al de ayer.
    /// </summary>
    private static List<IEnergyUsage.DailyUsage> Plano(int dias, double kwh, double puntaKwh = 0,
        double picoKw = 0, Guid? zona = null) =>
        Enumerable.Range(1, dias)
            .Select(d => new IEnergyUsage.DailyUsage(Medidor, Local, zona ?? Zona, Dia(d), kwh,
                puntaKwh, picoKw))
            .ToList();

    private void Consumo(List<IEnergyUsage.DailyUsage> dias) =>
        _energia.DailyUsageAsync(Usuario, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(dias);

    // --------------------------------------------------------------- vacio

    [Fact]
    public async Task RecalcularAsync_NoMeasuredConsumption_FindsNothingAndSavesNothing()
    {
        Consumo(new List<IEnergyUsage.DailyUsage>());

        var hallazgos = await _motor.RecalcularAsync(Usuario);

        Assert.Empty(hallazgos.Anomalias);
        Assert.Empty(hallazgos.Recomendaciones);
        Assert.Empty(hallazgos.Proyecciones);
        await _anomalies.DidNotReceive().SaveAsync(Arg.Any<Anomaly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecalcularAsync_OnlyTodayMeasured_IgnoresTheDayInProgress()
    {
        // El dia en curso esta a medias: compararlo con medias de dias completos
        // daria un desvio enorme a media manana y ninguno a medianoche.
        Consumo(new List<IEnergyUsage.DailyUsage>
        {
            new(Medidor, Local, Zona, Dia(0), 400, 100, 90)
        });

        var hallazgos = await _motor.RecalcularAsync(Usuario);

        Assert.Empty(hallazgos.Anomalias);
        Assert.Empty(hallazgos.Recomendaciones);
    }

    // ------------------------------------------------------------ anomalias

    [Fact]
    public async Task RecalcularAsync_SpikeWithEnoughHistory_RaisesAConsumptionSpike()
    {
        // El ultimo dia cerrado dobla la media de los ocho anteriores.
        Consumo(Plano(8, 100).Concat(new[]
        {
            new IEnergyUsage.DailyUsage(Medidor, Local, Zona, Dia(1), 200, 0, 0)
        }).ToList());

        var hallazgos = await _motor.RecalcularAsync(Usuario);

        var anomalia = Assert.Single(hallazgos.Anomalias);
        Assert.Equal("consumption_spike", anomalia.AnomalyType);
        Assert.Equal(Medidor.ToString(), anomalia.DeviceId);
        Assert.Equal(200, anomalia.ActualKwh);
        Assert.Equal(100, anomalia.ExpectedKwh);
        Assert.Equal("high", anomalia.Severity);
        Assert.Contains("100%", anomalia.Description);
    }

    [Fact]
    public async Task RecalcularAsync_SpikeWithoutEnoughHistory_StaysQuiet()
    {
        // Con cuatro dias medidos no hay con que afirmar que el quinto es raro.
        Consumo(Plano(4, 100).Concat(new[]
        {
            new IEnergyUsage.DailyUsage(Medidor, Local, Zona, Dia(1), 400, 0, 0)
        }).ToList());

        Assert.Empty((await _motor.RecalcularAsync(Usuario)).Anomalias);
    }

    [Fact]
    public async Task RecalcularAsync_SmallDeviationOnASmallMeter_StaysQuiet()
    {
        // Un 30 % mas sobre 10 kWh son 3 kWh: cierto, pero no es noticia.
        Consumo(Plano(8, 10).Concat(new[]
        {
            new IEnergyUsage.DailyUsage(Medidor, Local, Zona, Dia(1), 13, 0, 0)
        }).ToList());

        Assert.Empty((await _motor.RecalcularAsync(Usuario)).Anomalias);
    }

    [Fact]
    public async Task RecalcularAsync_SpikeAlreadyReportedToday_DoesNotRepeatIt()
    {
        _anomalies.FindByUserIdAsync(Usuario, Arg.Any<CancellationToken>()).Returns(
            new List<Anomaly>
            {
                Anomaly.Detect(Usuario, Medidor.ToString(), "consumption_spike", "ya avisada",
                    "high", 200, 100)
            });
        Consumo(Plano(8, 100).Concat(new[]
        {
            new IEnergyUsage.DailyUsage(Medidor, Local, Zona, Dia(1), 200, 0, 0)
        }).ToList());

        Assert.Empty((await _motor.RecalcularAsync(Usuario)).Anomalias);
    }

    // ------------------------------------------------------- recomendaciones

    [Fact]
    public async Task RecalcularAsync_OneZoneTakesMostOfTheSite_SuggestsReviewingThatZone()
    {
        Consumo(Plano(10, 100, picoKw: 10, zona: Zona));

        var recomendaciones = (await _motor.RecalcularAsync(Usuario)).Recomendaciones;

        var zona = Assert.Single(recomendaciones, r => r.RecommendationType == "zone_concentration");
        Assert.Equal("Revisa Camaras de frio en Sede Miraflores", zona.Title);
        Assert.Contains("100%", zona.Description);
        // Un 8 % de los 1000 kWh del periodo, a precio de fuera de punta.
        Assert.Equal(80, zona.EstimatedSavingKwh);
        Assert.Equal(Math.Round(80 * 0.2395, 2), zona.EstimatedSavingAmount);
        Assert.Equal("PEN", zona.Currency);
    }

    [Fact]
    public async Task RecalcularAsync_MuchConsumptionInThePeakWindow_SuggestsShiftingLoad()
    {
        // Un tercio del consumo cae en la franja cara.
        Consumo(Plano(10, 90, puntaKwh: 30, picoKw: 10));

        var recomendaciones = (await _motor.RecalcularAsync(Usuario)).Recomendaciones;

        var punta = Assert.Single(recomendaciones, r => r.RecommendationType == "peak_shift");
        Assert.Equal("Desplaza consumo fuera de punta en Sede Miraflores", punta.Title);
        Assert.Contains("18:00", punta.Description);
        // La quinta parte de los 300 kWh en punta, al diferencial de precio.
        Assert.Equal(60, punta.EstimatedSavingKwh);
        Assert.Equal(Math.Round(60 * (0.2810 - 0.2395), 2), punta.EstimatedSavingAmount);
    }

    [Fact]
    public async Task RecalcularAsync_LittleConsumptionInThePeakWindow_DoesNotSuggestShiftingLoad()
    {
        Consumo(Plano(10, 100, puntaKwh: 10, picoKw: 10));

        Assert.DoesNotContain((await _motor.RecalcularAsync(Usuario)).Recomendaciones,
            r => r.RecommendationType == "peak_shift");
    }

    [Fact]
    public async Task RecalcularAsync_PeakDemandNearTheContractedPower_SuggestsStaggeringStartups()
    {
        // 110 kW sobre 120 contratados: el 92 % de lo contratado.
        Consumo(Plano(10, 100, picoKw: 110));

        var recomendaciones = (await _motor.RecalcularAsync(Usuario)).Recomendaciones;

        var pico = Assert.Single(recomendaciones, r => r.RecommendationType == "demand_peak");
        Assert.Equal("Escalona los arranques en Sede Miraflores", pico.Title);
        Assert.Contains("110 kW sobre 120 kW contratados (92%)", pico.Description);
        // Rebajar el pico un 10 % ahorra ese 10 % del cargo por potencia.
        Assert.Equal(Math.Round(120 * (110d / 120d) * 0.10 * 58.40, 2), pico.EstimatedSavingAmount);
        // El ahorro no es en energia: el cargo se paga por el pico.
        Assert.Equal(0, pico.EstimatedSavingKwh);
    }

    [Fact]
    public async Task RecalcularAsync_PeakDemandWellBelowTheContractedPower_StaysQuiet()
    {
        Consumo(Plano(10, 100, picoKw: 60));

        Assert.DoesNotContain((await _motor.RecalcularAsync(Usuario)).Recomendaciones,
            r => r.RecommendationType == "demand_peak");
    }

    [Fact]
    public async Task RecalcularAsync_RecommendationAlreadyMadeToday_DoesNotRepeatIt()
    {
        _recommendations.FindByUserIdAsync(Usuario, Arg.Any<CancellationToken>()).Returns(
            new List<Recommendation>
            {
                Recommendation.Create(Usuario, null, "zone_concentration",
                    "Revisa Camaras de frio en Sede Miraflores", "ya propuesta", 80, 19.16, "PEN")
            });
        Consumo(Plano(10, 100, picoKw: 10));

        Assert.DoesNotContain((await _motor.RecalcularAsync(Usuario)).Recomendaciones,
            r => r.RecommendationType == "zone_concentration");
    }

    [Fact]
    public async Task RecalcularAsync_ConsumptionWithoutASite_ProducesNoRecommendations()
    {
        // Sin local no hay tarifa ni potencia contratada con la que justificar
        // nada, y una recomendacion sin numero no se aplica.
        Consumo(new List<IEnergyUsage.DailyUsage>
        {
            new(Medidor, null, null, Dia(1), 500, 200, 110)
        });

        Assert.Empty((await _motor.RecalcularAsync(Usuario)).Recomendaciones);
    }

    // ------------------------------------------------------------ proyeccion

    [Fact]
    public async Task RecalcularAsync_MeasuredConsumption_ProjectsTheMonthlyBillOfTheSite()
    {
        Consumo(Plano(10, 100, puntaKwh: 20, picoKw: 110));

        var proyeccion = Assert.Single((await _motor.RecalcularAsync(Usuario)).Proyecciones);

        Assert.Equal(Local.ToString(), proyeccion.SiteId);
        // El pico no se extrapola: el cargo lo fija el que ya ocurrio.
        Assert.Equal(110, proyeccion.EstimatedMaxDemandKw);
        // Diez dias medidos extrapolados a los dias del mes.
        var factor = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month) / 10d;
        Assert.Equal(Math.Round(200 * factor, 2), proyeccion.EstimatedKwhPeak);
        Assert.Equal(Math.Round(800 * factor, 2), proyeccion.EstimatedKwhOffPeak);
        // Con diez dias medidos el margen declarado es del 10 %.
        Assert.Equal(10, proyeccion.ErrorMarginPercentage);
    }

    [Fact]
    public async Task RecalcularAsync_FewDaysMeasured_DeclaresAWiderErrorMargin()
    {
        Consumo(Plano(2, 100, picoKw: 10));

        var proyeccion = Assert.Single((await _motor.RecalcularAsync(Usuario)).Proyecciones);

        // Dos dias dan para una orientacion, no para una cifra: el margen que se
        // guarda es lo que evita presentarla como si lo fuera.
        Assert.Equal(40, proyeccion.ErrorMarginPercentage);
    }

    [Fact]
    public async Task RecalcularAsync_SiteAlreadyProjectedToday_DoesNotProjectItAgain()
    {
        _predictions.FindByUserIdAsync(Usuario, Arg.Any<CancellationToken>()).Returns(
            new List<BillPrediction>
            {
                BillPrediction.Create(Usuario, DateTime.UtcNow.Year, DateTime.UtcNow.Month,
                    DateTime.UtcNow, DateTime.UtcNow, 1000, 500, "PEN", 0.2395, 10,
                    siteId: Local.ToString())
            });
        Consumo(Plano(10, 100, picoKw: 10));

        Assert.Empty((await _motor.RecalcularAsync(Usuario)).Proyecciones);
    }
}
