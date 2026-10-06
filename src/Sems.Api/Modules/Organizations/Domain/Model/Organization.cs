using Sems.Api.Shared.Errors;

namespace Sems.Api.Modules.Organizations.Domain.Model;

/// <summary>
/// Raiz del agregado Organization: la empresa que contrata el servicio.
///
/// <para>Es el nivel mas alto del segmento: una cadena de supermercados es una
/// organizacion con varios locales. Una tienda independiente tambien es una
/// organizacion, solo que con un local. Modelarlo asi evita tener dos caminos
/// distintos segun el tamano del cliente.</para>
/// </summary>
public class Organization
{
    public Guid OrganizationId { get; private set; }

    /// <summary>Razon social, la que figura en la factura.</summary>
    public string LegalName { get; private set; } = string.Empty;

    /// <summary>Nombre comercial, el que ve el publico. Puede no existir.</summary>
    public string? TradeName { get; private set; }

    /// <summary>RUC. Identifica al contribuyente y no se repite.</summary>
    public string TaxId { get; private set; } = string.Empty;

    public BusinessType BusinessType { get; private set; }

    public OrgStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>Constructor sin argumentos que exige EF Core al rehidratar.</summary>
    private Organization()
    {
    }

    /// <summary>Unica forma correcta de dar de alta una organizacion.</summary>
    public static Organization Register(string? legalName, string? tradeName, string? taxId,
        BusinessType businessType)
    {
        if (string.IsNullOrWhiteSpace(legalName))
        {
            throw AppException.Validation("legal_name is required");
        }

        var ruc = (taxId ?? string.Empty).Trim();
        if (ruc.Length == 0)
        {
            throw AppException.Validation("tax_id is required");
        }

        // El RUC peruano son 11 digitos. Se comprueba la forma, no la existencia:
        // validar contra SUNAT es una llamada a un tercero y no puede decidir si
        // un alta se acepta o no.
        if (ruc.Length != 11 || !ruc.All(char.IsDigit))
        {
            throw AppException.Validation("tax_id must be 11 digits");
        }

        var ahora = DateTime.UtcNow;
        return new Organization
        {
            OrganizationId = Guid.NewGuid(),
            LegalName = legalName.Trim(),
            TradeName = string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim(),
            TaxId = ruc,
            BusinessType = businessType,
            Status = OrgStatus.ACTIVE,
            CreatedAt = ahora,
            UpdatedAt = ahora
        };
    }

    public void UpdateDetails(string? legalName, string? tradeName, BusinessType businessType)
    {
        if (string.IsNullOrWhiteSpace(legalName))
        {
            throw AppException.Validation("legal_name is required");
        }

        LegalName = legalName.Trim();
        TradeName = string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim();
        BusinessType = businessType;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Suspend()
    {
        Status = OrgStatus.SUSPENDED;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        if (Status == OrgStatus.ARCHIVED)
        {
            throw AppException.Validation("an archived organization cannot be reactivated");
        }
        Status = OrgStatus.ACTIVE;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = OrgStatus.ARCHIVED;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsActive => Status == OrgStatus.ACTIVE;
}
