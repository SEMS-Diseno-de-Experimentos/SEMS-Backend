using Sems.Api.Shared.Errors;

namespace Sems.Api.Modules.Organizations.Domain.Model;

/// <summary>Tipo de negocio del establecimiento.</summary>
/// <remarks>
/// No es decorativo: el perfil de consumo cambia radicalmente entre uno y otro.
/// Un supermercado gasta la mayor parte en refrigeracion, que funciona las 24
/// horas; un restaurante concentra el gasto en cocina y en las horas de
/// servicio. La analitica compara cada local contra otros de su mismo tipo.
/// </remarks>
public enum BusinessType
{
    SUPERMARKET,
    CONVENIENCE_STORE,
    DEPARTMENT_STORE,
    RESTAURANT,
    WAREHOUSE,
    OTHER
}

/// <summary>Zona dentro de un local.</summary>
/// <remarks>
/// Sustituye a la "habitacion" del segmento residencial. La diferencia no es
/// solo de nombre: una camara frigorifica no se apaga de noche y una sala de
/// ventas si, asi que una misma alerta de "consumo fuera de horario" es normal
/// en una y sospechosa en la otra.
/// </remarks>
public enum ZoneType
{
    SALES_FLOOR,
    COLD_STORAGE,
    WAREHOUSE,
    KITCHEN,
    OFFICES,
    HVAC,
    PARKING,
    OTHER
}

/// <summary>
/// Categoria tarifaria del suministro, segun el pliego de OSINERGMIN.
/// </summary>
/// <remarks>
/// <para>Aqui esta la diferencia economica de fondo con el segmento
/// residencial. Una vivienda en BT5B paga un precio unico por kWh. Un
/// establecimiento en BT3, BT4, MT2 o MT3 paga tres cosas distintas: la energia
/// consumida en hora punta, la consumida fuera de punta, y un cargo por
/// potencia sobre la demanda maxima registrada en el mes.</para>
///
/// <para>Ese cargo por potencia es lo que sorprende a un local: un pico de
/// quince minutos al arrancar los compresores encarece la factura del mes
/// entero, aunque la energia total no haya subido. Sin modelarlo, la prediccion
/// de factura se queda corta y las recomendaciones de ahorro apuntan al sitio
/// equivocado.</para>
/// </remarks>
public enum TariffCategory
{
    /// <summary>Baja tension sin cargo por potencia. Solo para locales muy pequenos.</summary>
    BT5B,

    BT3,
    BT4,
    MT2,
    MT3
}

/// <summary>Estado de una organizacion, local o zona.</summary>
public enum OrgStatus
{
    ACTIVE,
    SUSPENDED,
    ARCHIVED
}

/// <summary>Papel de una persona dentro de una organizacion.</summary>
/// <remarks>
/// Un establecimiento no lo lleva una sola persona, a diferencia de una casa.
/// El alcance de cada papel se resuelve en <see cref="Membership"/>: el
/// administrador manda en toda la cadena, el supervisor solo en su local.
/// </remarks>
public enum MembershipRole
{
    /// <summary>Gestiona la organizacion entera: locales, personas y suscripcion.</summary>
    ORG_ADMIN,

    /// <summary>Gestiona el local que tiene asignado.</summary>
    SUPERVISOR,

    /// <summary>Solo consulta.</summary>
    OPERATOR
}

public static class OrganizationEnums
{
    public static BusinessType ToBusinessType(string? value) =>
        Parse<BusinessType>(value, "business_type");

    public static ZoneType ToZoneType(string? value) =>
        Parse<ZoneType>(value, "zone_type");

    public static TariffCategory ToTariffCategory(string? value) =>
        Parse<TariffCategory>(value, "tariff_category");

    public static MembershipRole ToMembershipRole(string? value) =>
        Parse<MembershipRole>(value, "role");

    public static OrgStatus ToOrgStatus(string? value) =>
        Parse<OrgStatus>(value, "status");

    /// <summary>
    /// Convierte texto no confiable en un valor del enum, o falla con un error
    /// que nombra el campo. Se hace en un solo sitio para que todos los enums
    /// del modulo rechacen la entrada invalida de la misma forma.
    /// </summary>
    private static T Parse<T>(string? value, string campo) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw AppException.Validation($"{campo} is required");
        }
        if (!Enum.TryParse<T>(value.Trim(), ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            throw AppException.Validation($"{campo} is invalid");
        }
        return parsed;
    }
}

/// <summary>
/// Si la categoria tarifaria cobra por potencia contratada ademas de por
/// energia.
/// </summary>
public static class TariffCategoryExtensions
{
    public static bool CobraPorPotencia(this TariffCategory categoria) =>
        categoria != TariffCategory.BT5B;
}
