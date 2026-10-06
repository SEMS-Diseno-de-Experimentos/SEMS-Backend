using Sems.Api.Modules.Energy.Domain.Model;
using Xunit;

namespace Sems.Api.Tests.Energy;

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
    public void FranjaDe_TuesdaySevenPmLocal_IsPeak()
    {
        // 19:00 en Peru son las 00:00 UTC del dia siguiente. Si se evaluara en
        // UTC saldria fuera de punta, que es justo lo contrario.
        var martes19hLocal = new DateTime(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(FranjaHoraria.PUNTA, HorarioPunta.FranjaDe(martes19hLocal));
    }

    [Fact]
    public void FranjaDe_TenAmLocal_IsOffPeak()
    {
        var martes10hLocal = new DateTime(2026, 9, 8, 15, 0, 0, DateTimeKind.Utc);

        Assert.Equal(FranjaHoraria.FUERA_DE_PUNTA, HorarioPunta.FranjaDe(martes10hLocal));
    }

    [Fact]
    public void FranjaDe_SundayEveningByDefault_IsPeak()
    {
        // El pliego fija la hora punta de 18:00 a 23:00 de cada dia del ano. La
        // exclusion de domingos existe, pero solo "a solicitud del cliente": no es
        // el caso general. Darla por concedida subestima la factura del domingo,
        // que para un supermercado es uno de sus dias de mayor afluencia.
        var domingo20hLocal = new DateTime(2026, 9, 14, 1, 0, 0, DateTimeKind.Utc);

        Assert.Equal(DayOfWeek.Sunday, HorarioPunta.AHoraLocal(domingo20hLocal).DayOfWeek);
        Assert.Equal(FranjaHoraria.PUNTA, HorarioPunta.FranjaDe(domingo20hLocal));
    }

    [Fact]
    public void FranjaDe_SundayEveningWithTheExclusion_IsOffPeak()
    {
        var domingo20hLocal = new DateTime(2026, 9, 14, 1, 0, 0, DateTimeKind.Utc);

        Assert.Equal(FranjaHoraria.FUERA_DE_PUNTA,
            HorarioPunta.FranjaDe(domingo20hLocal, excluyeDomingos: true));
    }

    [Fact]
    public void FranjaDe_WeekdayWithTheSundayExclusion_IsStillPeak()
    {
        // Un suministro con la exclusion concedida sigue teniendo punta de lunes a
        // sabado: la excepcion es solo para domingos y feriados.
        var martes19hLocal = new DateTime(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(FranjaHoraria.PUNTA,
            HorarioPunta.FranjaDe(martes19hLocal, excluyeDomingos: true));
    }

    [Fact]
    public void FranjaDe_ElevenPmSharp_IsAlreadyOffPeak()
    {
        // El limite superior no se incluye: la franja es [18:00, 23:00).
        var martes23hLocal = new DateTime(2026, 9, 9, 4, 0, 0, DateTimeKind.Utc);

        Assert.Equal(FranjaHoraria.FUERA_DE_PUNTA, HorarioPunta.FranjaDe(martes23hLocal));
    }

    // ------------------------------------------------------ cargo por potencia

    [Fact]
    public void Calculate_SameEnergyWithAPeakOf150KwOver120Contracted_KeepsEnergyCostAndAddsOver3000()
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
    public void CostoDePotencia_DemandOf150KwWith120Contracted_ChargesTheExcessAtPenaltyPrice()
    {
        var tarifa = TarifaMT2();

        var factura = tarifa.Calcular(0m, 0m, demandaMaximaKw: 150m, potenciaContratadaKw: 120m);

        // 120 kW al precio normal y 30 kW al de penalizacion, no 150 al normal.
        Assert.Equal(120m * 58.40m + 30m * 87.60m, factura.CostoPotencia);
        Assert.True(factura.HayExcesoDePotencia);
        Assert.Equal(30m, factura.ExcesoDePotenciaKw);
    }

    [Theory]
    [InlineData(100, false, 0)]
    [InlineData(120, false, 0)]
    [InlineData(120.01, true, 0.01)]
    public void Calculate_DemandAroundTheContractedPower_ReportsExcessOnlyAboveIt(decimal demand,
        bool expectedExcess, decimal expectedExcessKw)
    {
        var factura = TarifaMT2().Calcular(0m, 0m, demand, potenciaContratadaKw: 120m);

        Assert.Equal(expectedExcess, factura.HayExcesoDePotencia);
        Assert.Equal(expectedExcessKw, factura.ExcesoDePotenciaKw);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5000)]
    [InlineData(250000)]
    public void CostoDePotencia_AnyEnergyConsumed_IsAlwaysTheSame(decimal kwh)
    {
        var factura = TarifaMT2().Calcular(kwh / 4m, kwh * 3m / 4m, demandaMaximaKw: 130m,
            potenciaContratadaKw: 120m);

        Assert.Equal(120m * 58.40m + 10m * 87.60m, factura.CostoPotencia);
    }

    [Fact]
    public void CostoDePotencia_WithoutRegisteredDemand_IsZero()
    {
        // Un local recien dado de alta todavia no tiene lecturas. Cobrarle
        // potencia sobre una demanda de cero seria inventarse un cargo.
        var factura = TarifaMT2().Calcular(0m, 0m, demandaMaximaKw: 0m,
            potenciaContratadaKw: 120m);

        Assert.Equal(0m, factura.CostoPotencia);
    }

    // -------------------------------------------------------------- la factura

    [Fact]
    public void Calculate_ConsumptionMovedOffPeak_LowersTheBill()
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

    [Theory]
    [InlineData(1, 0, 0)]          // 13.08 + 2.35: el caso que descuadraba un centimo
    [InlineData(0, 1, 0)]
    [InlineData(3, 7, 0)]
    [InlineData(1000, 2000, 50)]
    [InlineData(6000, 24000, 150)]
    [InlineData(12000, 48000, 280)]
    public void Calculate_AnyConsumption_TotalEqualsRoundedSubtotalPlusRoundedIgv(decimal kwhPeak,
        decimal kwhOffPeak, decimal demand)
    {
        var factura = TarifaMT2().Calcular(kwhPeak, kwhOffPeak, demand, potenciaContratadaKw: 120m);

        Assert.Equal(factura.Subtotal + factura.Igv, factura.Total);
        Assert.Equal(Math.Round(factura.Subtotal, 2), factura.Subtotal);
        Assert.Equal(Math.Round(factura.Igv, 2), factura.Igv);
    }

    [Fact]
    public void Calculate_OneKwhAtPeak_ShowsTheTotalAsTheSumOfItsParts()
    {
        var factura = TarifaMT2().Calcular(1m, 0m, 0m, 120m);

        Assert.Equal(13.08m, factura.Subtotal);
        Assert.Equal(2.35m, factura.Igv);
        Assert.Equal(15.43m, factura.Total);
    }

    [Fact]
    public void Calculate_Section526Example_ReturnsTheDocumentedBreakdown()
    {
        // Local en MT2 con 250 kW contratados, 12 000 kWh en punta, 48 000 fuera
        // de punta y una demanda maxima de 280 kW.
        var factura = TarifaMT2().Calcular(12000m, 48000m, 280m, 250m);

        Assert.Equal(14868.00m, factura.CostoEnergia);
        Assert.Equal(17228.00m, factura.CostoPotencia);
        Assert.Equal(32108.80m, factura.Subtotal);
        Assert.Equal(5779.58m, factura.Igv);
        Assert.Equal(37888.38m, factura.Total);
        Assert.Equal(53.7m, factura.PesoDeLaPotencia);
    }
}
