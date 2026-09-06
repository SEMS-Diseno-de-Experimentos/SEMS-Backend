namespace Sems.Api.Modules.Energy.Domain.Model;

/// <summary>
/// Franja horaria de un consumo.
/// </summary>
/// <remarks>
/// En el segmento residencial no existia: una vivienda paga lo mismo a
/// cualquier hora. Un establecimiento en tarifa comercial no.
/// </remarks>
public enum FranjaHoraria
{
    /// <summary>Hora punta.</summary>
    PUNTA,

    /// <summary>Fuera de punta.</summary>
    FUERA_DE_PUNTA
}

/// <summary>
/// Reglas del horario de punta del sistema electrico peruano.
/// </summary>
/// <remarks>
/// <para>La hora punta va de 18:00 a 23:00, de lunes a sabado. Los domingos no
/// tienen punta.</para>
///
/// <para>Para un supermercado esto no es un detalle contable: coincide con la
/// hora de mayor afluencia, cuando estan encendidas todas las cajas, la
/// iluminacion y el aire. Saber cuanto del consumo cae en esa franja es lo que
/// permite recomendar mover cargas desplazables (bombeo, carga de baterias,
/// precongelado) a otro horario.</para>
/// </remarks>
public static class HorarioPunta
{
    public const int HoraInicio = 18;
    public const int HoraFin = 23;

    /// <summary>Franja a la que pertenece un instante.</summary>
    /// <remarks>
    /// Se evalua en hora local de Peru, no en UTC. Las lecturas se guardan en
    /// UTC, y Peru esta en UTC-5: sin convertir, un consumo de las 19:00 locales
    /// se registraria como las 00:00 del dia siguiente y se facturaria como
    /// fuera de punta, que es justo lo contrario de lo que es.
    /// </remarks>
    public static FranjaHoraria FranjaDe(DateTime instanteUtc)
    {
        var local = AHoraLocal(instanteUtc);

        if (local.DayOfWeek == DayOfWeek.Sunday)
        {
            return FranjaHoraria.FUERA_DE_PUNTA;
        }

        return local.Hour >= HoraInicio && local.Hour < HoraFin
            ? FranjaHoraria.PUNTA
            : FranjaHoraria.FUERA_DE_PUNTA;
    }

    public static bool EsHoraPunta(DateTime instanteUtc) =>
        FranjaDe(instanteUtc) == FranjaHoraria.PUNTA;

    /// <summary>Pasa un instante UTC a hora de Peru (UTC-5, sin horario de verano).</summary>
    public static DateTime AHoraLocal(DateTime instanteUtc) =>
        DateTime.SpecifyKind(instanteUtc, DateTimeKind.Utc).AddHours(-5);
}

/// <summary>
/// Tarifa electrica comercial: precios de energia por franja mas cargos por
/// potencia.
/// </summary>
/// <remarks>
/// <para>Es la diferencia de fondo con la tarifa de una vivienda, que solo
/// tenia un precio por kWh. Aqui la factura suma tres conceptos distintos y el
/// segundo, la potencia, no depende de cuanta energia se gasto sino de cual fue
/// el pico mas alto del mes.</para>
///
/// <para>Los importes son referenciales y los entrega el adaptador del
/// proveedor: este tipo solo sabe combinarlos.</para>
/// </remarks>
public sealed record CommercialTariff(
    string Provider,
    string TariffCategory,
    string Currency,
    decimal EnergiaPuntaPorKwh,
    decimal EnergiaFueraDePuntaPorKwh,
    decimal PotenciaPorKwMes,
    decimal ExcesoDePotenciaPorKwMes,
    decimal CargoFijoMensual,
    decimal Igv,
    DateTime Timestamp)
{
    /// <summary>Coste de la energia consumida, sin impuestos.</summary>
    public decimal CostoDeEnergia(decimal kwhPunta, decimal kwhFueraDePunta) =>
        kwhPunta * EnergiaPuntaPorKwh + kwhFueraDePunta * EnergiaFueraDePuntaPorKwh;

    /// <summary>
    /// Coste de la potencia del mes, sin impuestos.
    /// </summary>
    /// <remarks>
    /// Se cobra sobre la demanda maxima registrada, y lo que exceda de la
    /// potencia contratada se cobra ademas a precio de penalizacion. Un pico de
    /// quince minutos encarece el mes entero: por eso conviene detectarlo y
    /// avisar, no solo mostrar el consumo acumulado.
    /// </remarks>
    public decimal CostoDePotencia(decimal demandaMaximaKw, decimal potenciaContratadaKw)
    {
        if (demandaMaximaKw <= 0)
        {
            return 0m;
        }

        var dentroDeContrato = Math.Min(demandaMaximaKw, potenciaContratadaKw);
        var exceso = Math.Max(0m, demandaMaximaKw - potenciaContratadaKw);

        return dentroDeContrato * PotenciaPorKwMes + exceso * ExcesoDePotenciaPorKwMes;
    }

    /// <summary>Desglose completo de la factura estimada.</summary>
    public BillBreakdown Calcular(decimal kwhPunta, decimal kwhFueraDePunta,
        decimal demandaMaximaKw, decimal potenciaContratadaKw)
    {
        var energia = CostoDeEnergia(kwhPunta, kwhFueraDePunta);
        var potencia = CostoDePotencia(demandaMaximaKw, potenciaContratadaKw);
        var subtotal = energia + potencia + CargoFijoMensual;
        var impuesto = subtotal * Igv;

        return new BillBreakdown(
            KwhPunta: kwhPunta,
            KwhFueraDePunta: kwhFueraDePunta,
            DemandaMaximaKw: demandaMaximaKw,
            PotenciaContratadaKw: potenciaContratadaKw,
            ExcesoDePotenciaKw: Math.Max(0m, demandaMaximaKw - potenciaContratadaKw),
            CostoEnergia: Redondear(energia),
            CostoPotencia: Redondear(potencia),
            CargoFijo: Redondear(CargoFijoMensual),
            Subtotal: Redondear(subtotal),
            Igv: Redondear(impuesto),
            Total: Redondear(subtotal + impuesto),
            Currency: Currency);
    }

    private static decimal Redondear(decimal valor) =>
        Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Desglose de la factura estimada de un local.
/// </summary>
/// <remarks>
/// Se devuelve entero, no solo el total, porque el valor para el cliente esta
/// en el desglose: ver que el cargo por potencia se come una parte de la
/// factura es lo que justifica actuar sobre los picos.
/// </remarks>
public sealed record BillBreakdown(
    decimal KwhPunta,
    decimal KwhFueraDePunta,
    decimal DemandaMaximaKw,
    decimal PotenciaContratadaKw,
    decimal ExcesoDePotenciaKw,
    decimal CostoEnergia,
    decimal CostoPotencia,
    decimal CargoFijo,
    decimal Subtotal,
    decimal Igv,
    decimal Total,
    string Currency)
{
    /// <summary>Que porcentaje del subtotal se lleva la potencia.</summary>
    public decimal PesoDeLaPotencia =>
        Subtotal <= 0 ? 0m : Math.Round(CostoPotencia / Subtotal * 100m, 1);

    /// <summary>Si se supero la potencia contratada.</summary>
    public bool HayExcesoDePotencia => ExcesoDePotenciaKw > 0;
}
