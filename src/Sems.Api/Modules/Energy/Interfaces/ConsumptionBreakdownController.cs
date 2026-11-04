using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Sems.Api.Modules.Energy.Application;
using Swashbuckle.AspNetCore.Annotations;

namespace Sems.Api.Modules.Energy.Interfaces;

/// <summary>
/// Reparto del consumo por zona y entre locales (US24, US26 y US39).
/// </summary>
/// <remarks>
/// Las rutas cuelgan de <c>/sites</c> y <c>/organizations</c> aunque vivan en
/// energia: quien pregunta piensa en el local, no en el modulo que guarda las
/// lecturas. Es el mismo criterio con el que alertas publica las reglas de
/// demanda bajo <c>/sites/{id}/demand-rules</c>.
/// </remarks>
[ApiController]
[Route("api/v1")]
[Tags("Consumption Breakdown")]
public sealed class ConsumptionBreakdownController : ControllerBase
{
    private readonly ConsumptionBreakdownService _breakdown;

    public ConsumptionBreakdownController(ConsumptionBreakdownService breakdown) =>
        _breakdown = breakdown;

    /// <summary>Usage of a site split across its zones.</summary>
    [HttpGet("sites/{siteId:guid}/zone-consumption")]
    public async Task<ZoneBreakdownResponse> ByZone(Guid siteId,
        [FromQuery] int days = ConsumptionBreakdownService.DiasPorDefecto) =>
        ZoneBreakdownResponse.From(await _breakdown.ByZoneAsync(siteId, days));

    /// <summary>Usage of each site of an organization, for comparison.</summary>
    [HttpGet("organizations/{organizationId:guid}/site-consumption")]
    public async Task<SiteComparisonResponse> BySite(Guid organizationId,
        [FromQuery] int days = ConsumptionBreakdownService.DiasPorDefecto) =>
        SiteComparisonResponse.From(await _breakdown.BySiteAsync(organizationId, days));

    /// <summary>Power the site is drawing right now.</summary>
    [HttpGet("sites/{siteId:guid}/consumption/current")]
    public async Task<CurrentConsumptionResponse> Current(Guid siteId) =>
        CurrentConsumptionResponse.From(await _breakdown.CurrentAsync(siteId));

    // Las respuestas viven aqui porque no las comparte nadie mas; el modulo de
    // energia responde en snake_case, como el resto de sus recursos.

    public sealed record ZoneShareResponse(
        [property: JsonPropertyName("zone_id")] string? ZoneId,
        [property: JsonPropertyName("zone_name")] string? ZoneName,
        [property: JsonPropertyName("zone_type")] string? ZoneType,
        [property: JsonPropertyName("meter_count")] int MeterCount,
        [property: JsonPropertyName("kwh")] double Kwh,
        [property: JsonPropertyName("share_percent")] double SharePercent,
        [property: JsonPropertyName("estimated_energy_cost")] decimal? EstimatedEnergyCost)
    {
        public static ZoneShareResponse From(ConsumptionBreakdownService.ZoneShare z) =>
            new(z.ZoneId?.ToString(), z.ZoneName, z.ZoneType, z.MeterCount, z.Kwh, z.SharePercent,
                z.EstimatedEnergyCost);
    }

    public sealed record ZoneBreakdownResponse(
        [property: JsonPropertyName("site_id")] string SiteId,
        [property: JsonPropertyName("site_name")] string SiteName,
        [property: JsonPropertyName("days")] int Days,
        [property: JsonPropertyName("from")] DateTime PeriodFrom,
        [property: JsonPropertyName("to")] DateTime PeriodTo,
        [property: JsonPropertyName("total_kwh")] double TotalKwh,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("zones")] List<ZoneShareResponse> Zones,
        [property: JsonPropertyName("unassigned")] ZoneShareResponse Unassigned)
    {
        public static ZoneBreakdownResponse From(ConsumptionBreakdownService.ZoneBreakdown b) =>
            new(b.SiteId.ToString(), b.SiteName, b.Days, b.From, b.To, b.TotalKwh, b.Currency,
                b.Zones.Select(ZoneShareResponse.From).ToList(),
                ZoneShareResponse.From(b.Unassigned));
    }

    public sealed record SiteShareResponse(
        [property: JsonPropertyName("site_id")] string SiteId,
        [property: JsonPropertyName("site_code")] string SiteCode,
        [property: JsonPropertyName("site_name")] string SiteName,
        [property: JsonPropertyName("contracted_power_kw")] decimal ContractedPowerKw,
        [property: JsonPropertyName("tariff_category")] string? TariffCategory,
        [property: JsonPropertyName("meter_count")] int MeterCount,
        [property: JsonPropertyName("kwh")] double Kwh,
        [property: JsonPropertyName("share_percent")] double SharePercent,
        [property: JsonPropertyName("estimated_energy_cost")] decimal? EstimatedEnergyCost,
        [property: JsonPropertyName("kwh_per_contracted_kw")] double? KwhPerContractedKw,
        [property: JsonPropertyName("peak_demand_kw")] double PeakDemandKw)
    {
        public static SiteShareResponse From(ConsumptionBreakdownService.SiteShare s) =>
            new(s.SiteId.ToString(), s.SiteCode, s.SiteName, s.ContractedPowerKw,
                s.TariffCategory, s.MeterCount, s.Kwh, s.SharePercent, s.EstimatedEnergyCost,
                s.KwhPerContractedKw, s.PeakDemandKw);
    }

    public sealed record SiteComparisonResponse(
        [property: JsonPropertyName("organization_id")] string OrganizationId,
        [property: JsonPropertyName("days")] int Days,
        [property: JsonPropertyName("from")] DateTime PeriodFrom,
        [property: JsonPropertyName("to")] DateTime PeriodTo,
        [property: JsonPropertyName("total_kwh")] double TotalKwh,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("sites")] List<SiteShareResponse> Sites)
    {
        public static SiteComparisonResponse From(ConsumptionBreakdownService.SiteComparison c) =>
            new(c.OrganizationId.ToString(), c.Days, c.From, c.To, c.TotalKwh, c.Currency,
                c.Sites.Select(SiteShareResponse.From).ToList());
    }

    public sealed record CurrentConsumptionResponse(
        [property: JsonPropertyName("site_id")] string SiteId,
        [property: JsonPropertyName("site_name")] string SiteName,
        [property: JsonPropertyName("meter_count")] int MeterCount,
        [property: JsonPropertyName("power_kw")] double PowerKw,
        [property: JsonPropertyName("contracted_power_kw")] decimal ContractedPowerKw,
        [property: JsonPropertyName("occupancy_percent")] double? OccupancyPercent,
        [property: JsonPropertyName("measured_at")] DateTime? MeasuredAt)
    {
        public static CurrentConsumptionResponse From(
            ConsumptionBreakdownService.CurrentConsumption c) =>
            new(c.SiteId.ToString(), c.SiteName, c.MeterCount, c.PowerKw, c.ContractedPowerKw,
                c.OccupancyPercent, c.MeasuredAt);
    }
}
