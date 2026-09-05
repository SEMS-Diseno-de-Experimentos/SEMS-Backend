using Sems.Api.Shared.Errors;

namespace Sems.Api.Modules.Devices.Domain.Model;

/// <summary>
/// Estado de un dispositivo.
///
/// <para>Un enum da la misma garantia que en Go se lograba con un tipo string
/// con nombre: el compilador impide pasar cualquier cadena donde se espera un
/// estado.</para>
/// </summary>
public enum DeviceStatus
{
    ACTIVE,
    INACTIVE,
    DISCONNECTED,
    REMOVED
}

/// <summary>Estado del vinculo entre un dispositivo y un usuario u hogar.</summary>
public enum BindingStatus
{
    LINKED,
    UNLINKED,
    PENDING
}

/// <summary>Protocolo por el que el dispositivo se comunica.</summary>
public enum ConnectionProtocol
{
    WIFI,
    BLUETOOTH
}

public static class DeviceEnums
{
    /// <summary>Convierte texto no confiable en un estado valido.</summary>
    public static DeviceStatus ToDeviceStatus(string? value)
    {
        if (Enum.TryParse<DeviceStatus>(value?.Trim(), ignoreCase: true, out var status)
            && Enum.IsDefined(status))
        {
            return status;
        }
        throw AppException.Validation("invalid device status");
    }

    public static BindingStatus ToBindingStatus(string? value)
    {
        if (Enum.TryParse<BindingStatus>(value?.Trim(), ignoreCase: true, out var status)
            && Enum.IsDefined(status))
        {
            return status;
        }
        throw AppException.Validation("invalid binding status");
    }

    public static ConnectionProtocol ToConnectionProtocol(string? value)
    {
        if (Enum.TryParse<ConnectionProtocol>(value?.Trim(), ignoreCase: true, out var protocol)
            && Enum.IsDefined(protocol))
        {
            return protocol;
        }
        throw AppException.Validation("connection_protocol is invalid");
    }

    /// <summary>
    /// Maquina de estados del dispositivo. El orden de las reglas importa:
    /// <list type="number">
    ///   <item>REMOVED es terminal: de ahi no se sale.</item>
    ///   <item>Cualquier dispositivo vivo puede pasar a REMOVED.</item>
    ///   <item>El resto de transiciones solo valen entre estados vivos.</item>
    /// </list>
    /// </summary>
    public static bool CanTransitionTo(this DeviceStatus current, DeviceStatus next)
    {
        if (current == DeviceStatus.REMOVED)
        {
            return false;
        }
        if (next == DeviceStatus.REMOVED)
        {
            return true;
        }
        return next is DeviceStatus.ACTIVE or DeviceStatus.INACTIVE or DeviceStatus.DISCONNECTED;
    }
}
