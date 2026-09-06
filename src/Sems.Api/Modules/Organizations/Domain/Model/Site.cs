using Sems.Api.Shared.Errors;

namespace Sems.Api.Modules.Organizations.Domain.Model;

/// <summary>
/// Raiz del agregado Site: un local fisico con su propio suministro electrico.
///
/// <para>Es la unidad que de verdad importa para la energia. Cada local tiene su
/// medidor, su contrato con la distribuidora y su factura, asi que es el nivel
/// al que se predice el gasto y se compara el rendimiento entre locales de la
/// misma cadena.</para>
///
/// <para>Es agregado aparte de <see cref="Organization"/> a proposito: una
/// cadena puede tener decenas de locales y cargarlos todos para tocar uno solo
/// seria un desperdicio. Se relacionan por identificador, no por referencia.</para>
/// </summary>
public class Site
{
    public Guid SiteId { get; private set; }

    public Guid OrganizationId { get; private set; }

    /// <summary>Codigo interno del local dentro de la cadena. Unico por organizacion.</summary>
    public string SiteCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Address { get; private set; }

    public string? District { get; private set; }

    /// <summary>Superficie en metros cuadrados. Permite comparar locales de distinto tamano.</summary>
    public decimal? FloorAreaM2 { get; private set; }

    /// <summary>
    /// Potencia contratada en kW.
    /// </summary>
    /// <remarks>
    /// Es el limite pactado con la distribuidora. Superarlo no corta la luz: se
    /// factura como exceso, y ese recargo es una de las fugas de dinero mas
    /// habituales en un local, porque no aparece en el consumo de energia sino
    /// en un cargo aparte que casi nadie mira.
    /// </remarks>
    public decimal ContractedPowerKw { get; private set; }

    public TariffCategory TariffCategory { get; private set; }

    public OrgStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>Constructor sin argumentos que exige EF Core al rehidratar.</summary>
    private Site()
    {
    }

    public static Site Register(Guid organizationId, string? siteCode, string? name,
        string? address, string? district, decimal? floorAreaM2,
        decimal contractedPowerKw, TariffCategory tariffCategory)
    {
        if (organizationId == Guid.Empty)
        {
            throw AppException.Validation("organization_id is required");
        }
        if (string.IsNullOrWhiteSpace(siteCode))
        {
            throw AppException.Validation("site_code is required");
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            throw AppException.Validation("name is required");
        }
        if (contractedPowerKw <= 0)
        {
            throw AppException.Validation("contracted_power_kw must be greater than zero");
        }
        if (floorAreaM2 is <= 0)
        {
            throw AppException.Validation("floor_area_m2 must be greater than zero");
        }

        var ahora = DateTime.UtcNow;
        return new Site
        {
            SiteId = Guid.NewGuid(),
            OrganizationId = organizationId,
            SiteCode = siteCode.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
            District = string.IsNullOrWhiteSpace(district) ? null : district.Trim(),
            FloorAreaM2 = floorAreaM2,
            ContractedPowerKw = contractedPowerKw,
            TariffCategory = tariffCategory,
            Status = OrgStatus.ACTIVE,
            CreatedAt = ahora,
            UpdatedAt = ahora
        };
    }

    public void UpdateDetails(string? name, string? address, string? district,
        decimal? floorAreaM2, decimal contractedPowerKw, TariffCategory tariffCategory)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw AppException.Validation("name is required");
        }
        if (contractedPowerKw <= 0)
        {
            throw AppException.Validation("contracted_power_kw must be greater than zero");
        }
        if (floorAreaM2 is <= 0)
        {
            throw AppException.Validation("floor_area_m2 must be greater than zero");
        }

        Name = name.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        District = string.IsNullOrWhiteSpace(district) ? null : district.Trim();
        FloorAreaM2 = floorAreaM2;
        ContractedPowerKw = contractedPowerKw;
        TariffCategory = tariffCategory;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = OrgStatus.ARCHIVED;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsActive => Status == OrgStatus.ACTIVE;

    /// <summary>
    /// Cuanto se pasa de la potencia contratada una demanda dada, en kW.
    /// Cero si no la supera.
    /// </summary>
    public decimal ExcesoDePotencia(decimal demandaMaximaKw) =>
        demandaMaximaKw > ContractedPowerKw ? demandaMaximaKw - ContractedPowerKw : 0m;
}

/// <summary>
/// Zona dentro de un local: sala de ventas, camaras, almacen, cocina, oficinas.
///
/// <para>Es una entidad del agregado <see cref="Site"/>, no una raiz: una zona
/// no existe fuera de su local y siempre se llega a ella a traves de el.</para>
/// </summary>
public class Zone
{
    public Guid ZoneId { get; private set; }

    public Guid SiteId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public ZoneType ZoneType { get; private set; }

    /// <summary>
    /// Si la zona funciona fuera del horario de atencion.
    /// </summary>
    /// <remarks>
    /// Lo usa el modulo de alertas: consumo de madrugada en una camara
    /// frigorifica es lo esperado, y en la sala de ventas es una luz o un equipo
    /// que alguien se dejo encendido. Sin este dato, la misma regla genera
    /// avisos falsos en una zona y se calla en la otra.
    /// </remarks>
    public bool OperatesOffHours { get; private set; }

    public OrgStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private Zone()
    {
    }

    public static Zone Register(Guid siteId, string? name, ZoneType zoneType, bool? operatesOffHours)
    {
        if (siteId == Guid.Empty)
        {
            throw AppException.Validation("site_id is required");
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            throw AppException.Validation("name is required");
        }

        return new Zone
        {
            ZoneId = Guid.NewGuid(),
            SiteId = siteId,
            Name = name.Trim(),
            ZoneType = zoneType,
            // Si no lo dicen, se deduce del tipo: las camaras y el aire
            // acondicionado no se apagan; el resto si.
            OperatesOffHours = operatesOffHours
                ?? zoneType is ZoneType.COLD_STORAGE or ZoneType.HVAC,
            Status = OrgStatus.ACTIVE,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateDetails(string? name, ZoneType zoneType, bool operatesOffHours)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw AppException.Validation("name is required");
        }
        Name = name.Trim();
        ZoneType = zoneType;
        OperatesOffHours = operatesOffHours;
    }

    public void Archive() => Status = OrgStatus.ARCHIVED;

    public bool IsActive => Status == OrgStatus.ACTIVE;
}
