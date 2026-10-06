using Sems.Api.Shared.Errors;

namespace Sems.Api.Modules.Organizations.Domain.Model;

/// <summary>
/// Vinculo entre una persona y una organizacion, con su papel y su alcance.
///
/// <para>Es lo que sustituye al usuario unico del segmento residencial. En una
/// casa, quien entra es el dueno y ve todo. En una cadena, el jefe de tienda de
/// Miraflores no tiene por que ver el consumo de San Isidro, y el operario no
/// tiene por que poder dar de baja un local.</para>
///
/// <para>El alcance se decide con <see cref="SiteId"/>: si es nulo, el vinculo
/// cubre la organizacion entera; si trae un local, solo ese.</para>
/// </summary>
public class Membership
{
    public Guid MembershipId { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid UserId { get; private set; }

    public MembershipRole Role { get; private set; }

    /// <summary>Local al que se limita el vinculo. Nulo = toda la organizacion.</summary>
    public Guid? SiteId { get; private set; }

    public OrgStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private Membership()
    {
    }

    public static Membership Grant(Guid organizationId, Guid userId, MembershipRole role, Guid? siteId)
    {
        if (organizationId == Guid.Empty)
        {
            throw AppException.Validation("organization_id is required");
        }
        if (userId == Guid.Empty)
        {
            throw AppException.Validation("user_id is required");
        }

        // El administrador manda en toda la cadena por definicion. Atarlo a un
        // local seria contradictorio, y ademas dejaria la organizacion sin nadie
        // que pueda dar de alta el siguiente local.
        if (role == MembershipRole.ORG_ADMIN && siteId is not null)
        {
            throw AppException.Validation("an ORG_ADMIN cannot be limited to a single site");
        }

        // Al reves tambien: un supervisor sin local asignado no supervisa nada,
        // y dejarlo pasar acabaria dandole acceso a toda la cadena por descuido.
        if (role == MembershipRole.SUPERVISOR && siteId is null)
        {
            throw AppException.Validation("a SUPERVISOR must be assigned to a site");
        }

        var ahora = DateTime.UtcNow;
        return new Membership
        {
            MembershipId = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            Role = role,
            SiteId = siteId,
            Status = OrgStatus.ACTIVE,
            CreatedAt = ahora,
            UpdatedAt = ahora
        };
    }

    public void ChangeRole(MembershipRole role, Guid? siteId)
    {
        if (role == MembershipRole.ORG_ADMIN && siteId is not null)
        {
            throw AppException.Validation("an ORG_ADMIN cannot be limited to a single site");
        }
        if (role == MembershipRole.SUPERVISOR && siteId is null)
        {
            throw AppException.Validation("a SUPERVISOR must be assigned to a site");
        }

        Role = role;
        SiteId = siteId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Revoke()
    {
        Status = OrgStatus.ARCHIVED;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsActive => Status == OrgStatus.ACTIVE;

    /// <summary>Si este vinculo permite ver o tocar el local indicado.</summary>
    public bool AlcanzaAlLocal(Guid siteId) =>
        IsActive && (SiteId is null || SiteId == siteId);

    /// <summary>Si este vinculo permite modificar, no solo consultar.</summary>
    public bool PuedeModificar =>
        IsActive && Role is MembershipRole.ORG_ADMIN or MembershipRole.SUPERVISOR;

    /// <summary>Si este vinculo permite gestionar locales y personas.</summary>
    public bool PuedeAdministrarLaOrganizacion =>
        IsActive && Role == MembershipRole.ORG_ADMIN;
}
