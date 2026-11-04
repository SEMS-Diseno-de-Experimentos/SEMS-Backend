using Sems.Api.Modules.Analytics.Domain.Model;
using Sems.Api.Modules.Analytics.Domain.Repositories;
using Sems.Api.Modules.Analytics.Domain.Services;

namespace Sems.Api.Modules.Analytics.Application;

/// <summary>
/// Produce las anomalias, las recomendaciones y la proyeccion de recibo a
/// partir del consumo medido (US35, US37 y US38).
/// </summary>
/// <remarks>
/// <para>Hasta ahora las tres cosas se podian guardar y consultar, pero nadie
/// las generaba: los endpoints de alta esperaban a un productor que no existia,
/// de modo que las pantallas estaban condenadas a salir vacias por mucho que el
/// cliente midiera. Esto es ese productor.</para>
///
/// <para>Son reglas explicitas y pocas, no un modelo. Esa es la eleccion
/// deliberada: una recomendacion que el cliente no entiende no la aplica, y una
/// anomalia que no sabe explicar por que lo es acaba ignorada. Cada regla de
/// aqui se puede defender en una frase, y el texto que genera dice el numero
/// que la disparo.</para>
/// </remarks>
public sealed class InsightEngine
{
    /// <summary>Ventana de historia que se mira. Un mes es el periodo del recibo.</summary>
    public const int Dias = 30;

    /// <summary>Dias minimos de historia antes de afirmar que algo es raro.</summary>
    private const int DiasMinimos = 7;

    /// <summary>Desviaciones tipicas por encima de su media que hacen sospechoso un dia.</summary>
    private const double Sigmas = 2.0;

    /// <summary>Y ademas tiene que separarse esto de la media, para no marcar ruido.</summary>
    private const double DesvioMinimoPorcentaje = 25.0;
    private const double DesvioMinimoKwh = 5.0;

    /// <summary>Parte del local a partir de la cual una zona merece que se la mire.</summary>
    private const double ZonaDominante = 0.40;

    /// <summary>Parte en hora punta a partir de la cual conviene desplazar carga.</summary>
    private const double PuntaAlta = 0.25;

    /// <summary>Ocupacion de la potencia contratada que justifica escalonar arranques.</summary>
    private const double PicoAlto = 0.85;

    private readonly IAnomalyRepository _anomalies;
    private readonly IRecommendationRepository _recommendations;
    private readonly AnalyticsService _analytics;
    private readonly IEnergyUsage _energia;
    private readonly ILogger<InsightEngine> _logger;

    public InsightEngine(IAnomalyRepository anomalies, IRecommendationRepository recommendations,
        AnalyticsService analytics, IEnergyUsage energia, ILogger<InsightEngine> logger)
    {
        _anomalies = anomalies;
        _recommendations = recommendations;
        _analytics = analytics;
        _energia = energia;
        _logger = logger;
    }

    /// <summary>Lo producido en una pasada.</summary>
    public sealed record Hallazgos(List<Anomaly> Anomalias, List<Recommendation> Recomendaciones,
        List<BillPrediction> Proyecciones);

    /// <summary>
    /// Recalcula los hallazgos del usuario con lo medido hasta hoy.
    /// </summary>
    /// <remarks>
    /// <para>Es idempotente dentro del dia: si ya se genero el mismo hallazgo
    /// hoy, no se duplica. Sin esa condicion, con una lectura cada pocos minutos
    /// la lista creceria hasta ser inservible.</para>
    ///
    /// <para>Solo se miran dias cerrados. El dia en curso esta a medias, y
    /// compararlo con medias de dias completos daria un desvio enorme a media
    /// manana y ninguno a medianoche. Ademas hace que el resultado no cambie
    /// dentro del mismo dia, que es lo que permite no duplicar hallazgos sin
    /// tener que reescribir los de antes.</para>
    /// </remarks>
    public async Task<Hallazgos> RecalcularAsync(string userId, CancellationToken ct = default)
    {
        var hoy = HoyEnPeru();
        var consumo = (await _energia.DailyUsageAsync(userId, Dias, ct))
            .Where(d => d.Day < hoy)
            .ToList();

        if (consumo.Count == 0)
        {
            return new Hallazgos(new(), new(), new());
        }

        var proyecciones = await ProyectarFacturaAsync(userId, consumo, ct);
        var anomalias = await DetectarAnomaliasAsync(userId, consumo, ct);
        var recomendaciones = await ProponerAhorrosAsync(userId, consumo, ct);

        _logger.LogInformation(
            "Hallazgos de {UserId}: {Anomalias} anomalia(s), {Recomendaciones} recomendacion(es), "
            + "{Proyecciones} proyeccion(es)",
            userId, anomalias.Count, recomendaciones.Count, proyecciones.Count);

        return new Hallazgos(anomalias, recomendaciones, proyecciones);
    }

    // ------------------------------------------------------------ proyeccion

    /// <summary>
    /// Proyecta el recibo del mes de cada local con lo medido hasta hoy (US38).
    /// </summary>
    /// <remarks>
    /// <para>La pantalla de analitica leia la proyeccion de la base de datos y
    /// nadie la escribia nunca, asi que mostraba una raya para siempre. El
    /// calculo ya existia en <c>ForecastSiteBillAsync</c>; lo que faltaba era
    /// que alguien le pasara el consumo medido.</para>
    ///
    /// <para>Se extrapola lo medido a los dias del mes: con una semana de datos
    /// la cifra es orientativa, y el margen de error que se guarda lo dice.</para>
    /// </remarks>
    private async Task<List<BillPrediction>> ProyectarFacturaAsync(string userId,
        List<IEnergyUsage.DailyUsage> consumo, CancellationToken ct)
    {
        var nuevas = new List<BillPrediction>();
        var hoy = HoyEnPeru();
        var yaProyectados = (await _analytics.PredictionsByUserAsync(userId, ct))
            .Where(p => DateOnly.FromDateTime(EnPeru(p.CreatedAt)) == hoy)
            .Select(p => p.SiteId)
            .Where(s => s is not null)
            .ToHashSet();

        foreach (var grupo in PorLocal(consumo))
        {
            var local = grupo.Key;
            if (yaProyectados.Contains(local.ToString()))
            {
                continue;
            }

            var precios = await _energia.PricesOfAsync(local, ct);
            if (precios is null || precios.ContractedPowerKw <= 0)
            {
                continue;
            }

            var dias = grupo.Value;
            var diasMedidos = dias.Select(d => d.Day).Distinct().Count();
            if (diasMedidos == 0)
            {
                continue;
            }

            var diasDelMes = DateTime.DaysInMonth(hoy.Year, hoy.Month);
            var factor = (double)diasDelMes / diasMedidos;
            var punta = dias.Sum(d => d.PeakKwh) * factor;
            var fuera = dias.Sum(d => d.Kwh - d.PeakKwh) * factor;

            // La demanda no se extrapola: el cargo lo fija el pico ya ocurrido,
            // y multiplicarlo por los dias que faltan no tendria sentido.
            var pico = dias.Max(d => d.MaxPowerKw);

            // Cuantos menos dias medidos, mas se parece esto a una apuesta.
            var margen = Math.Max(5, Math.Min(40, 100.0 / Math.Max(1, diasMedidos)));

            var inicio = new DateTime(hoy.Year, hoy.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddHours(5);
            var fin = inicio.AddDays(diasDelMes).AddMinutes(-1);

            nuevas.Add(await _analytics.ForecastSiteBillAsync(userId, local.ToString(),
                precios.TariffCategory, (double)precios.ContractedPowerKw, hoy.Year, hoy.Month,
                inicio, fin, Redondea(punta), Redondea(fuera), pico, Redondea(margen), ct));
        }

        return nuevas;
    }

    // ------------------------------------------------------------- anomalias

    /// <summary>
    /// Un dia es anomalo cuando se sale del comportamiento del propio medidor
    /// (US37).
    /// </summary>
    /// <remarks>
    /// Se compara cada medidor consigo mismo y no con los demas: una camara de
    /// frio consume mucho mas que la iluminacion de la oficina sin que eso sea
    /// un problema. Lo que importa es el cambio respecto a lo habitual de ese
    /// punto de medida.
    /// </remarks>
    private async Task<List<Anomaly>> DetectarAnomaliasAsync(string userId,
        List<IEnergyUsage.DailyUsage> consumo, CancellationToken ct)
    {
        var hoy = HoyEnPeru();
        var yaAvisados = (await _anomalies.FindByUserIdAsync(userId, ct))
            .Where(a => DateOnly.FromDateTime(EnPeru(a.DetectedAt)) == hoy)
            .Select(a => a.DeviceId)
            .ToHashSet();

        var nuevas = new List<Anomaly>();
        foreach (var grupo in consumo.GroupBy(d => d.MeterId))
        {
            var dias = grupo.OrderBy(d => d.Day).ToList();
            if (dias.Count < DiasMinimos + 1)
            {
                continue;
            }

            var ultimo = dias[^1];
            if (yaAvisados.Contains(ultimo.MeterId.ToString()))
            {
                continue;
            }

            var anteriores = dias.Take(dias.Count - 1).ToList();
            var media = anteriores.Average(d => d.Kwh);
            if (media <= 0)
            {
                continue;
            }

            var sigma = Math.Sqrt(anteriores.Average(d => Math.Pow(d.Kwh - media, 2)));
            var exceso = ultimo.Kwh - media;
            var desvio = exceso / media * 100.0;

            var fueraDeRango = exceso > Sigmas * sigma
                               && desvio >= DesvioMinimoPorcentaje
                               && exceso >= DesvioMinimoKwh;
            if (!fueraDeRango)
            {
                continue;
            }

            var descripcion = string.Format(
                "El {0:yyyy-MM-dd} midio {1:0.#} kWh, un {2:0}% por encima de los {3:0.#} kWh que "
                + "viene consumiendo de media en los ultimos {4} dias.",
                ultimo.Day, ultimo.Kwh, desvio, media, anteriores.Count);
            var severidad = desvio >= 60 ? "high" : desvio >= 40 ? "medium" : "low";

            // El campo se llama DeviceId por historia; aqui lleva el medidor,
            // que es el punto de medida del que salio el consumo.
            nuevas.Add(await _anomalies.SaveAsync(
                Anomaly.Detect(userId, ultimo.MeterId.ToString(), "consumption_spike", descripcion,
                    severidad, Redondea(ultimo.Kwh), Redondea(media)), ct));
        }

        return nuevas;
    }

    // -------------------------------------------------------- recomendaciones

    /// <summary>
    /// Propone donde hay ahorro, con el numero que lo justifica (US35).
    /// </summary>
    /// <remarks>
    /// Tres reglas, cada una atada a como se factura un suministro comercial:
    /// donde se concentra el consumo, cuanto cae en la franja cara, y si el pico
    /// de potencia esta cerca de lo contratado.
    /// </remarks>
    private async Task<List<Recommendation>> ProponerAhorrosAsync(string userId,
        List<IEnergyUsage.DailyUsage> consumo, CancellationToken ct)
    {
        var hoy = HoyEnPeru();
        var yaPropuestas = (await _recommendations.FindByUserIdAsync(userId, ct))
            .Where(r => DateOnly.FromDateTime(EnPeru(r.GeneratedAt)) == hoy)
            .Select(r => $"{r.RecommendationType}|{r.Title}")
            .ToHashSet();

        var nuevas = new List<Recommendation>();
        foreach (var grupo in PorLocal(consumo))
        {
            var local = grupo.Key;
            var dias = grupo.Value;

            var precios = await _energia.PricesOfAsync(local, ct);
            if (precios is null)
            {
                continue;
            }

            var totalKwh = dias.Sum(d => d.Kwh);
            if (totalKwh <= 0)
            {
                continue;
            }
            var puntaKwh = dias.Sum(d => d.PeakKwh);

            Agregar(nuevas, await ZonaDominanteAsync(userId, local, dias, totalKwh, precios,
                yaPropuestas, ct));
            Agregar(nuevas, await ConsumoEnPuntaAsync(userId, totalKwh, puntaKwh, precios,
                yaPropuestas, ct));
            Agregar(nuevas, await PicoDePotenciaAsync(userId, dias, precios, yaPropuestas, ct));
        }

        return nuevas;
    }

    /// <summary>Regla 1: una zona se lleva la mayor parte del local.</summary>
    private async Task<Recommendation?> ZonaDominanteAsync(string userId, Guid local,
        List<IEnergyUsage.DailyUsage> dias, double totalKwh, IEnergyUsage.SitePrices precios,
        HashSet<string> yaPropuestas, CancellationToken ct)
    {
        var porZona = dias.Where(d => d.ZoneId is not null)
            .GroupBy(d => d.ZoneId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(d => d.Kwh));

        if (porZona.Count == 0)
        {
            return null;
        }

        var mayor = porZona.OrderByDescending(p => p.Value).First();
        if (mayor.Value / totalKwh < ZonaDominante)
        {
            return null;
        }

        var zona = (await _energia.ZonesOfAsync(local, ct)).FirstOrDefault(z => z.ZoneId == mayor.Key);
        if (zona is null)
        {
            return null;
        }

        var titulo = $"Revisa {zona.Name} en {precios.SiteName}";
        if (!yaPropuestas.Add($"zone_concentration|{titulo}"))
        {
            return null;
        }

        var parte = mayor.Value / totalKwh * 100.0;

        // Un 8% es lo que suele dar revisar cierres, sondas y ciclos de
        // desescarche en una instalacion que nadie ha tocado en meses. Es una
        // estimacion prudente y se presenta como tal.
        var ahorroKwh = mayor.Value * 0.08;
        var ahorro = Math.Round(ahorroKwh * (double)precios.OffPeakPerKwh, 2);

        var descripcion = string.Format(
            "{0} concentra el {1:0}% del consumo del local ({2:0} kWh en el periodo). Una revision "
            + "de equipos y horarios en esa zona es donde mas rinde.",
            zona.Name, parte, mayor.Value);

        return await _recommendations.SaveAsync(
            Recommendation.Create(userId, null, "zone_concentration", titulo, descripcion,
                Redondea(ahorroKwh), ahorro, precios.Currency), ct);
    }

    /// <summary>Regla 2: demasiado consumo en la franja cara.</summary>
    private async Task<Recommendation?> ConsumoEnPuntaAsync(string userId, double totalKwh,
        double puntaKwh, IEnergyUsage.SitePrices precios, HashSet<string> yaPropuestas,
        CancellationToken ct)
    {
        var parte = puntaKwh / totalKwh;
        if (parte < PuntaAlta)
        {
            return null;
        }

        // Desplazar la quinta parte de lo que hoy cae en punta: lo que suele
        // poderse mover sin tocar el horario comercial (prerrefrigerar antes de
        // las 18:00, retrasar lavados y hornos).
        var movible = puntaKwh * 0.20;
        var diferencia = (double)(precios.PeakPerKwh - precios.OffPeakPerKwh);
        var ahorro = Math.Round(movible * diferencia, 2);
        if (ahorro <= 0)
        {
            return null;
        }

        var titulo = $"Desplaza consumo fuera de punta en {precios.SiteName}";
        if (!yaPropuestas.Add($"peak_shift|{titulo}"))
        {
            return null;
        }

        var descripcion = string.Format(
            "El {0:0}% del consumo cae entre las 18:00 y las 23:00, que es la franja cara "
            + "({1}/kWh frente a {2}/kWh). Mover una quinta parte a otra hora ahorra sin consumir "
            + "menos.",
            parte * 100, precios.PeakPerKwh, precios.OffPeakPerKwh);

        return await _recommendations.SaveAsync(
            Recommendation.Create(userId, null, "peak_shift", titulo, descripcion,
                Redondea(movible), ahorro, precios.Currency), ct);
    }

    /// <summary>Regla 3: el pico de potencia roza lo contratado.</summary>
    private async Task<Recommendation?> PicoDePotenciaAsync(string userId,
        List<IEnergyUsage.DailyUsage> dias, IEnergyUsage.SitePrices precios,
        HashSet<string> yaPropuestas, CancellationToken ct)
    {
        var pico = dias.Max(d => d.MaxPowerKw);
        var contratada = (double)precios.ContractedPowerKw;
        if (contratada <= 0 || pico <= 0)
        {
            return null;
        }

        var ocupacion = pico / contratada;
        if (ocupacion < PicoAlto)
        {
            return null;
        }

        var titulo = $"Escalona los arranques en {precios.SiteName}";
        if (!yaPropuestas.Add($"demand_peak|{titulo}"))
        {
            return null;
        }

        // Rebajar el pico un 10% ahorra ese 10% del cargo por potencia del mes,
        // que se paga por el pico y no por la energia.
        var ahorro = Math.Round(contratada * ocupacion * 0.10 * (double)precios.PowerPerKwMonth, 2);

        var descripcion = string.Format(
            "El pico medido fue de {0:0.#} kW sobre {1:0.#} kW contratados ({2:0}%). El cargo por "
            + "potencia del mes lo fija ese pico, asi que arrancar los equipos escalonados rebaja "
            + "la factura entera.",
            pico, contratada, ocupacion * 100);

        return await _recommendations.SaveAsync(
            Recommendation.Create(userId, null, "demand_peak", titulo, descripcion, 0, ahorro,
                precios.Currency), ct);
    }

    // ---------------------------------------------------------------- apoyo

    /// <summary>Peru esta en UTC-5 todo el ano: el dia natural se corta ahi.</summary>
    private static readonly TimeSpan Peru = TimeSpan.FromHours(-5);

    private static DateTime EnPeru(DateTime instanteUtc) => instanteUtc + Peru;

    private static DateOnly HoyEnPeru() => DateOnly.FromDateTime(EnPeru(DateTime.UtcNow));

    /// <summary>Agrupa por local, dejando fuera el consumo sin local asignado.</summary>
    private static List<KeyValuePair<Guid, List<IEnergyUsage.DailyUsage>>> PorLocal(
        List<IEnergyUsage.DailyUsage> consumo) =>
        consumo.Where(d => d.SiteId is not null)
            .GroupBy(d => d.SiteId!.Value)
            .Select(g => new KeyValuePair<Guid, List<IEnergyUsage.DailyUsage>>(g.Key, g.ToList()))
            .ToList();

    private static void Agregar(List<Recommendation> destino, Recommendation? r)
    {
        if (r is not null)
        {
            destino.Add(r);
        }
    }

    private static double Redondea(double v) => Math.Round(v, 2);
}
