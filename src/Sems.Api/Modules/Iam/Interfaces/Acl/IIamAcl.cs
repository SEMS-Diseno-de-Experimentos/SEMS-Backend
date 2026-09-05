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
}
