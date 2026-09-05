package com.sems.iam.interfaces.acl;

import java.util.Optional;
import java.util.UUID;

/**
 * Capa anticorrupcion del bounded context IAM.
 *
 * <p>Es la unica puerta por la que otros modulos preguntan por un usuario. No
 * expone el agregado ni las entidades de IAM: devuelve solo el dato concreto que
 * el otro contexto necesita, de modo que un cambio interno en IAM no se propaga.
 *
 * <p>Esto reemplaza a la tabla {@code user_contacts} del disenio de
 * microservicios, que existia unicamente porque el servicio de alertas no podia
 * consultar a IAM y tenia que replicar los correos escuchando eventos. Al vivir
 * ambos en el mismo proceso, esa duplicacion de estado desaparece.
 */
public interface IamAcl {

    /** Correo del usuario, o vacio si no existe. */
    Optional<String> emailOf(UUID userId);
}
