using Sems.Api.Modules.Energy.Domain.Model;
using Xunit;

namespace Sems.Api.Tests;

/// <summary>
/// Tarifa comercial: franjas horarias y cargo por potencia.
///
/// <para>Es la parte del cambio de segmento con consecuencias en dinero. Un
/// error aqui no da ningun sintoma visible: la aplicacion sigue mostrando una
/// factura estimada, solo que equivocada, y nadie lo nota hasta que llega el
/// recibo real.</para>
/// </summary>
public class CommercialTariffTests
{
    private static CommercialTariff TarifaMT2() => new(
        Provider: "Plus Energia",
        TariffCategory: "MT2",
        Currency: "PEN",
        EnergiaPuntaPorKwh: 0.2810m,
        EnergiaFueraDePuntaPorKwh: 0.2395m,
        PotenciaPorKwMes: 58.40m,
        ExcesoDePotenciaPorKwMes: 87.60m,
        CargoFijoMensual: 12.80m,
        Igv: 0.18m,
        Timestamp: DateTime.UtcNow);

    // ------------------------------------------------------- horario de punta

    [Fact]
    public void Las_siete_de_la_tarde_de_un_martes_es_hora_punta()
    {
        // 19:00 en Peru son las 00:00 UTC del dia siguiente. Si se evaluara en
        // UTC saldria fuera de punta, que es justo lo contrario.
        var martes19hLocal = new DateTime(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(FranjaHoraria.PUNTA, HorarioPunta.FranjaDe(martes19hLocal));
    }

    [Fact]
    public void Las_diez_de_la_manana_no_es_hora_punta()
    {
        var martes10hLocal = new DateTime(2026, 9, 8, 15, 0, 0, DateTimeKind.Utc);

        Assert.Equal(FranjaHoraria.FUERA_DE_PUNTA, HorarioPunta.FranjaDe(martes10hLocal));
    }

    [Fact]
    public void El_domingo_no_tiene_hora_punta()
    {
        // Domingo 20:00 en Peru: dentro de la franja horaria, pero es domingo.
        var domingo20hLocal = new DateTime(2026, 9, 14, 1, 0, 0, DateTimeKind.Utc);

        Assert.Equal(DayOfWeek.Sunday, HorarioPunta.AHoraLocal(domingo20hLocal).DayOfWeek);
        Assert.Equal(FranjaHoraria.FUERA_DE_PUNTA, HorarioPunta.FranjaDe(domingo20hLocal));
    }

    [Fact]
    public void Las_veintitres_en_punto_ya_esta_fuera_de_punta()
    {
        // El limite superior no se incluye: la franja es [18:00, 23:00).
        var martes23hLocal = new DateTime(2026, 9, 9, 4, 0, 0, DateTimeKind.Utc);

        Assert.Equal(FranjaHoraria.FUERA_DE_PUNTA, HorarioPunta.FranjaDe(martes23hLocal));
    }

    // ------------------------------------------------------ cargo por potencia

    [Fact]
    public void Un_pico_puntual_encarece_el_mes_entero_sin_gastar_mas_energia()
    {
        // Este es el motivo de modelar la potencia. Mismo consumo de energia,
        // misma factura de energia, y sin embargo el total sube mas de tres mil
        // soles solo por el pico.
        var tarifa = TarifaMT2();

        var sinPico = tarifa.Calcular(kwhPunta: 6000m, kwhFueraDePunta: 24000m,
            demandaMaximaKw: 120m, potenciaContratadaKw: 120m);
        var conPico = tarifa.Calcular(kwhPunta: 6000m, kwhFueraDePunta: 24000m,
            demandaMaximaKw: 150m, potenciaContratadaKw: 120m);

        Assert.Equal(sinPico.CostoEnergia, conPico.CostoEnergia);
        Assert.True(conPico.Total > sinPico.Total + 3000m,
            $"el pico deberia costar mas de S/3000; costo {conPico.Total - sinPico.Total:N2}");
    }

    [Fact]
    public void El_exceso_sobre_lo_contratado_se_cobra_a_precio_de_penalizacion()
    {
        var tarifa = TarifaMT2();

        var factura = tarifa.Calcular(0m, 0m, demandaMaximaKw: 150m, potenciaContratadaKw: 120m);

        // 120 kW al precio normal y 30 kW al de penalizacion, no 150 al normal.
        var esperado = 120m * 58.40m + 30m * 87.60m;
        Assert.Equal(esperado, factura.CostoPotencia);
        Assert.True(factura.HayExcesoDePotencia);
        Assert.Equal(30m, factura.ExcesoDePotenciaKw);
    }

    [Fact]
    public void Sin_superar_lo_contratado_no_hay_penalizacion()
    {
        var factura = TarifaMT2().Calcular(0m, 0m, demandaMaximaKw: 100m,
            potenciaContratadaKw: 120m);

        Assert.False(factura.HayExcesoDePotencia);
        Assert.Equal(0m, factura.ExcesoDePotenciaKw);
        Assert.Equal(100m * 58.40m, factura.CostoPotencia);
    }

    // -------------------------------------------------------------- la factura

    [Fact]
    public void Mover_consumo_fuera_de_punta_abarata_la_factura()
    {
        // Es la recomendacion principal que la aplicacion le dara a un local:
        // desplazar cargas que admiten horario, como el bombeo o el
        // precongelado.
        var tarifa = TarifaMT2();

        var muchaPunta = tarifa.Calcular(12000m, 18000m, 120m, 120m);
        var pocaPunta = tarifa.Calcular(3000m, 27000m, 120m, 120m);

        Assert.True(pocaPunta.Total < muchaPunta.Total);
        // La energia total es la misma: solo cambia en que franja se consume.
        Assert.Equal(30000m, muchaPunta.KwhPunta + muchaPunta.KwhFueraDePunta);
        Assert.Equal(30000m, pocaPunta.KwhPunta + pocaPunta.KwhFueraDePunta);
    }

    [Fact]
    public void El_igv_se_aplica_sobre_el_subtotal_y_el_total_cuadra()
    {
        var factura = TarifaMT2().Calcular(1000m, 2000m, 50m, 100m);

        Assert.Equal(factura.CostoEnergia + factura.CostoPotencia + factura.CargoFijo,
            factura.Subtotal);
        Assert.Equal(Math.Round(factura.Subtotal * 0.18m, 2, MidpointRounding.AwayFromZero),
            factura.Igv);
        Assert.Equal(factura.Subtotal + factura.Igv, factura.Total);
    }

    [Fact]
    public void Sin_demanda_registrada_no_se_cobra_potencia()
    {
        // Un local recien dado de alta todavia no tiene lecturas. Cobrarle
        // potencia sobre una demanda de cero seria inventarse un cargo.
        var factura = TarifaMT2().Calcular(0m, 0m, demandaMaximaKw: 0m,
            potenciaContratadaKw: 120m);

        Assert.Equal(0m, factura.CostoPotencia);
    }
}
