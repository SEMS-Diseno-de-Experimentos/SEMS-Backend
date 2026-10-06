using Sems.Api.Shared.Errors;

namespace Sems.Api.Modules.Devices.Domain.Model;

/// <summary>
/// Raiz del agregado Device.
///
/// <para>Toda modificacion pasa por sus metodos, de modo que las reglas de
/// negocio no se pueden saltar: el estado invalido no es representable desde
/// fuera del agregado. Los <c>private set</c> son lo que sostiene esa
/// garantia.</para>
/// </summary>
public class Device
{
    public Guid DeviceId { get; private set; }

    public string ExternalDeviceCode { get; private set; } = string.Empty;

    public Guid UserId { get; private set; }

    /// <summary>Local en el que esta instalado. Obligatorio.</summary>
    /// <remarks>
    /// El medidor, el contrato y la factura son del local, no de la persona.
    /// Sin este dato no se puede saber a que suministro imputar el consumo, que
    /// es justo lo que se hace con el en el segmento de establecimientos.
    /// </remarks>
    public Guid SiteId { get; private set; }

    /// <summary>Zona concreta dentro del local. Opcional.</summary>
    /// <remarks>
    /// Se admite vacia porque al dar de alta un local puede que aun no esten
    /// definidas las zonas, y bloquear el alta del medidor por eso seria
    /// molesto. En cuanto existe, permite separar el consumo de las camaras del
    /// de la sala de ventas.
    /// </remarks>
    public Guid? ZoneId { get; private set; }

    public string DeviceName { get; private set; } = string.Empty;

    public string DeviceType { get; private set; } = string.Empty;

    public string? Brand { get; private set; }

    public string? Model { get; private set; }

    public ConnectionProtocol ConnectionProtocol { get; private set; }

    public DeviceStatus Status { get; private set; }

    public DateTime RegisteredAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>Constructor sin argumentos que exige EF Core al rehidratar.</summary>
    private Device()
    {
    }

    /// <summary>
    /// Unica forma correcta de crear un dispositivo nuevo. Valida cada campo
    /// obligatorio antes de construir, de modo que un Device recien creado
    /// siempre es valido.
    /// </summary>
    public static Device Register(string? externalCode, Guid userId, Guid siteId, Guid? zoneId,
        string? name, string? deviceType, string? brand, string? model, ConnectionProtocol protocol)
    {
        if (string.IsNullOrWhiteSpace(externalCode))
        {
            throw AppException.Validation("external_device_code is required");
        }
        if (userId == Guid.Empty)
        {
            throw AppException.Validation("user_id is required");
        }
        if (siteId == Guid.Empty)
        {
            throw AppException.Validation("site_id is required");
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            throw AppException.Validation("device_name is required");
        }
        if (string.IsNullOrWhiteSpace(deviceType))
        {
            throw AppException.Validation("device_type is required");
        }

        var now = DateTime.UtcNow;
        return new Device
        {
            DeviceId = Guid.NewGuid(),
            ExternalDeviceCode = externalCode.Trim(),
            UserId = userId,
            SiteId = siteId,
            ZoneId = zoneId == Guid.Empty ? null : zoneId,
            DeviceName = name.Trim(),
            DeviceType = deviceType.Trim(),
            Brand = NormalizeOptional(brand),
            Model = NormalizeOptional(model),
            ConnectionProtocol = protocol,
            Status = DeviceStatus.ACTIVE,
            RegisteredAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Un dispositivo eliminado queda congelado y ya no admite ediciones.</summary>
    public void UpdateDetails(string? name, string? deviceType, string? brand, string? model,
        ConnectionProtocol protocol, Guid? zoneId)
    {
        if (IsRemoved)
        {
            throw AppException.Conflict("removed devices cannot be updated");
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            throw AppException.Validation("device_name is required");
        }
        if (string.IsNullOrWhiteSpace(deviceType))
        {
            throw AppException.Validation("device_type is required");
        }

        DeviceName = name.Trim();
        DeviceType = deviceType.Trim();
        Brand = NormalizeOptional(brand);
        Model = NormalizeOptional(model);
        ConnectionProtocol = protocol;
        // La zona si se puede cambiar: un equipo se traslada de la sala de
        // ventas al almacen sin dejar de ser el mismo equipo. El local no, que
        // seria otro suministro y rompería la continuidad del historico.
        ZoneId = zoneId == Guid.Empty ? null : zoneId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>La transicion la decide el value object, no el agregado.</summary>
    public void ChangeStatus(DeviceStatus next)
    {
        if (!Status.CanTransitionTo(next))
        {
            throw AppException.Conflict("invalid device status transition");
        }
        Status = next;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Borrado logico: la fila permanece, solo cambia el estado.</summary>
    public void Remove() => ChangeStatus(DeviceStatus.REMOVED);

    public void EnsureCanBeBound()
    {
        if (IsRemoved)
        {
            throw AppException.Conflict("removed devices cannot be linked");
        }
    }

    public void EnsureCanUpdateConfiguration()
    {
        if (IsRemoved)
        {
            throw AppException.Conflict("removed devices cannot update configuration");
        }
    }

    public bool IsRemoved => Status == DeviceStatus.REMOVED;

    /// <summary>Trata "en blanco" y "no informado" como lo mismo: ausencia de valor.</summary>
    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
