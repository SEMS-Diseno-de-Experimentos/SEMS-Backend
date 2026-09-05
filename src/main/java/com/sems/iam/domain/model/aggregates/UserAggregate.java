package com.sems.iam.domain.model.aggregates;

import com.sems.iam.domain.model.valueobjects.EmailAddress;
import com.sems.iam.domain.model.valueobjects.RoleName;
import java.time.Instant;
import java.util.HashSet;
import java.util.Set;
import java.util.UUID;

/**
 * UserAggregate is the "aggregate root" of our domain. In Domain-Driven Design
 * (DDD) the aggregate root is the main object that owns a cluster of related
 * data and protects its rules, so every change to a user must go through this
 * class instead of editing the fields from outside.
 *
 * Notice this is a PLAIN domain class: it has no Spring or JPA annotations on
 * purpose. Keeping the domain free of framework code makes the business rules
 * easy to read and to unit test.

/**
 * UserAggregate - Agregado de dominio que representa un usuario en el sistema IAM.
 * 
 * Esta clase encapsula toda la información y el comportamiento relacionado con un usuario,
 * incluyendo su identidad, credenciales, roles asignados y auditoría de cambios.
 * 
 * Patrón: Domain-Driven Design - Aggregate Root
 * Responsabilidades:
 * - Mantener la integridad de los datos del usuario
 * - Gestionar la asignación de roles
 * - Registrar cambios con timestamps
 * 
 * @author SEMS Team
 * @version 1.0

 */
public class UserAggregate {
    // Most fields are "final": once the user is built they never change, which
    // makes the object safer and easier to reason about. Only updatedAt is
    // mutable because it changes whenever the user is modified.
    private final UUID userId;            // unique identity of the user
    private final EmailAddress emailAddress; // a value object, already validated
    private final String passwordHash;    // we store the HASH, never the raw password
    private final Set<RoleName> roles;    // the roles the user has (a Set = no duplicates)
    private final Instant createdAt;
    private Instant updatedAt;

    /**
     * Constructor de UserAggregate.
     * 
     * @param userId Identificador único del usuario
     * @param emailAddress Dirección de correo electrónico del usuario
     * @param passwordHash Hash de la contraseña del usuario
     * @param roles Conjunto inicial de roles del usuario
     * @param createdAt Timestamp de creación
     * @param updatedAt Timestamp de última actualización
     */
    public UserAggregate(UUID userId, EmailAddress emailAddress, String passwordHash, Set<RoleName> roles, Instant createdAt, Instant updatedAt) {
        this.userId = userId;
        this.emailAddress = emailAddress;
        this.passwordHash = passwordHash;
        // We copy the incoming set into a new HashSet ("defensive copy"). If we
        // stored the caller's set directly, they could keep modifying our roles
        // from the outside and break the aggregate's encapsulation.
        this.roles = new HashSet<>(roles);
        this.createdAt = createdAt;
        this.updatedAt = updatedAt;
    }


    /**
     * Adds a role to the user. Because roles is a Set, adding a role the user
     * already has simply does nothing. We also refresh updatedAt to record that
     * the aggregate changed.

    /**
     * Asigna un nuevo rol al usuario y registra la actualización.
     * 
     * Este método implementa la lógica de negocio para añadir un rol, garantizando
     * que se actualice el timestamp de modificación.
     * 
     * @param roleName El rol a asignar al usuario

     */
    public void assignRole(RoleName roleName) {
        roles.add(roleName);
        updatedAt = Instant.now();
    }

    // The methods below are simple "getters" that expose the state in a
    // read-only way. roles() returns Set.copyOf(...) (an immutable copy) so that
    // callers can read the roles but cannot modify the internal set directly.
    public UUID userId() { return userId; }
    public EmailAddress emailAddress() { return emailAddress; }
    public String passwordHash() { return passwordHash; }
    public Set<RoleName> roles() { return Set.copyOf(roles); }
    public Instant createdAt() { return createdAt; }
    public Instant updatedAt() { return updatedAt; }
}
