namespace Sems.Api.Modules.Iam.Interfaces.Acl;

/// <summary>
/// Capa anticorrupcion del bounded context IAM.
///
/// <para>Es la unica puerta por la que otros modulos preguntan por un usuario.
/// No expone el agregado ni las entidades de IAM: devuelve solo el dato concreto
/// que el otro contexto necesita, de modo que un cambio interno en IAM no se
/// propaga.</para>
///
/// <para>Esto reemplaza a la tabla <c>user_contacts</c> del disenio de
/// microservicios, que existia unicamente porque el servicio de alertas no podia
/// consultar a IAM y tenia que replicar los correos escuchando eventos. Al vivir
/// ambos en el mismo proceso, esa duplicacion de estado desaparece.</para>
/// </summary>
public interface IIamAcl
{
    /// <summary>Correo del usuario, o null si no existe.</summary>
    Task<string?> EmailOfAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Identificador de quien usa ese correo, o null si no hay nadie.
    /// </summary>
    /// <remarks>
    /// Lo necesita organizaciones para dar acceso por correo: quien administra
    /// una cadena conoce el correo de sus companeros, no su UUID. Pedirle el
    /// identificador convertia la pantalla de accesos en algo que nadie podia
    /// usar sin consultar la base de datos.
    /// </remarks>
    Task<Guid?> UserIdOfAsync(string? emailAddress, CancellationToken ct = default);

    /// <summary>Correos de varios usuarios de una vez, indexados por su identificador.</summary>
    /// <remarks>
    /// Va en lote porque el listado de accesos los pide todos juntos, y hacerlo
    /// uno a uno seria una consulta por fila.
    /// </remarks>
    Task<Dictionary<Guid, string>> EmailsOfAsync(IReadOnlyCollection<Guid> userIds,
        CancellationToken ct = default);
}
