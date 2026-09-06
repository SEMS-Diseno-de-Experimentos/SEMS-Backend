using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Sems.Api.Modules.Organizations.Domain.Model;

namespace Sems.Api.Modules.Organizations.Interfaces;

/// <summary>
/// Contrato JSON del modulo de organizaciones, en snake_case.
/// </summary>
/// <remarks>
/// Se elige snake_case por ser el que usan la mayoria de modulos (energia,
/// analitica, alertas y pagos). Modulo nuevo, criterio unico: aqui no hay
/// historia previa que respetar.
///
/// Ojo con los atributos de validacion: en un record van como
/// <c>[Required]</c>, sin el prefijo <c>property:</c>. Con el prefijo ASP.NET
/// los ignora en silencio y el endpoint acepta cuerpos incompletos.
/// </remarks>
public static class OrganizationResources
{
    // ------------------------------------------------------------- peticiones

    public sealed record CreateOrganizationRequest(
        [property: JsonPropertyName("legal_name")]
        [Required(ErrorMessage = "is required")] string LegalName,
        [property: JsonPropertyName("trade_name")] string? TradeName,
        [property: JsonPropertyName("tax_id")]
        [Required(ErrorMessage = "is required")] string TaxId,
        [property: JsonPropertyName("business_type")]
        [Required(ErrorMessage = "is required")] string BusinessType,
        [property: JsonPropertyName("owner_user_id")]
        [Required(ErrorMessage = "is required")] string OwnerUserId);

    public sealed record UpdateOrganizationRequest(
        [property: JsonPropertyName("legal_name")]
        [Required(ErrorMessage = "is required")] string LegalName,
        [property: JsonPropertyName("trade_name")] string? TradeName,
        [property: JsonPropertyName("business_type")]
        [Required(ErrorMessage = "is required")] string BusinessType);

    public sealed record CreateSiteRequest(
        [property: JsonPropertyName("site_code")]
        [Required(ErrorMessage = "is required")] string SiteCode,
        [property: JsonPropertyName("name")]
        [Required(ErrorMessage = "is required")] string Name,
        [property: JsonPropertyName("address")] string? Address,
        [property: JsonPropertyName("district")] string? District,
        [property: JsonPropertyName("floor_area_m2")] decimal? FloorAreaM2,
        [property: JsonPropertyName("contracted_power_kw")]
        [Required(ErrorMessage = "is required")] decimal ContractedPowerKw,
        [property: JsonPropertyName("tariff_category")]
        [Required(ErrorMessage = "is required")] string TariffCategory);

    public sealed record UpdateSiteRequest(
        [property: JsonPropertyName("name")]
        [Required(ErrorMessage = "is required")] string Name,
        [property: JsonPropertyName("address")] string? Address,
        [property: JsonPropertyName("district")] string? District,
        [property: JsonPropertyName("floor_area_m2")] decimal? FloorAreaM2,
        [property: JsonPropertyName("contracted_power_kw")]
        [Required(ErrorMessage = "is required")] decimal ContractedPowerKw,
        [property: JsonPropertyName("tariff_category")]
        [Required(ErrorMessage = "is required")] string TariffCategory);

    public sealed record CreateZoneRequest(
        [property: JsonPropertyName("name")]
        [Required(ErrorMessage = "is required")] string Name,
        [property: JsonPropertyName("zone_type")]
        [Required(ErrorMessage = "is required")] string ZoneType,
        [property: JsonPropertyName("operates_off_hours")] bool? OperatesOffHours);

    public sealed record UpdateZoneRequest(
        [property: JsonPropertyName("name")]
        [Required(ErrorMessage = "is required")] string Name,
        [property: JsonPropertyName("zone_type")]
        [Required(ErrorMessage = "is required")] string ZoneType,
        [property: JsonPropertyName("operates_off_hours")] bool OperatesOffHours);

    public sealed record GrantMembershipRequest(
        [property: JsonPropertyName("user_id")]
        [Required(ErrorMessage = "is required")] string UserId,
        [property: JsonPropertyName("role")]
        [Required(ErrorMessage = "is required")] string Role,
        [property: JsonPropertyName("site_id")] string? SiteId);

    // ------------------------------------------------------------- respuestas

    public sealed record OrganizationResource(
        [property: JsonPropertyName("organization_id")] string OrganizationId,
        [property: JsonPropertyName("legal_name")] string LegalName,
        [property: JsonPropertyName("trade_name")] string? TradeName,
        [property: JsonPropertyName("tax_id")] string TaxId,
        [property: JsonPropertyName("business_type")] string BusinessType,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt,
        [property: JsonPropertyName("updated_at")] DateTime UpdatedAt)
    {
        public static OrganizationResource From(Organization o) => new(
            o.OrganizationId.ToString(), o.LegalName, o.TradeName, o.TaxId,
            o.BusinessType.ToString(), o.Status.ToString(), o.CreatedAt, o.UpdatedAt);
    }

    public sealed record SiteResource(
        [property: JsonPropertyName("site_id")] string SiteId,
        [property: JsonPropertyName("organization_id")] string OrganizationId,
        [property: JsonPropertyName("site_code")] string SiteCode,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("address")] string? Address,
        [property: JsonPropertyName("district")] string? District,
        [property: JsonPropertyName("floor_area_m2")] decimal? FloorAreaM2,
        [property: JsonPropertyName("contracted_power_kw")] decimal ContractedPowerKw,
        [property: JsonPropertyName("tariff_category")] string TariffCategory,
        [property: JsonPropertyName("charges_for_demand")] bool ChargesForDemand,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt,
        [property: JsonPropertyName("updated_at")] DateTime UpdatedAt)
    {
        public static SiteResource From(Site s) => new(
            s.SiteId.ToString(), s.OrganizationId.ToString(), s.SiteCode, s.Name, s.Address,
            s.District, s.FloorAreaM2, s.ContractedPowerKw, s.TariffCategory.ToString(),
            s.TariffCategory.CobraPorPotencia(), s.Status.ToString(), s.CreatedAt, s.UpdatedAt);
    }

    public sealed record ZoneResource(
        [property: JsonPropertyName("zone_id")] string ZoneId,
        [property: JsonPropertyName("site_id")] string SiteId,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("zone_type")] string ZoneType,
        [property: JsonPropertyName("operates_off_hours")] bool OperatesOffHours,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt)
    {
        public static ZoneResource From(Zone z) => new(
            z.ZoneId.ToString(), z.SiteId.ToString(), z.Name, z.ZoneType.ToString(),
            z.OperatesOffHours, z.Status.ToString(), z.CreatedAt);
    }

    public sealed record MembershipResource(
        [property: JsonPropertyName("membership_id")] string MembershipId,
        [property: JsonPropertyName("organization_id")] string OrganizationId,
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("site_id")] string? SiteId,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt)
    {
        public static MembershipResource From(Membership m) => new(
            m.MembershipId.ToString(), m.OrganizationId.ToString(), m.UserId.ToString(),
            m.Role.ToString(), m.SiteId?.ToString(), m.Status.ToString(), m.CreatedAt);
    }

    /// <summary>Organizacion con el papel que tiene ahi quien pregunta.</summary>
    public sealed record MyOrganizationResource(
        [property: JsonPropertyName("organization")] OrganizationResource Organization,
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("site_id")] string? SiteId)
    {
        public static MyOrganizationResource From(Organization o, Membership m) => new(
            OrganizationResource.From(o), m.Role.ToString(), m.SiteId?.ToString());
    }
}
