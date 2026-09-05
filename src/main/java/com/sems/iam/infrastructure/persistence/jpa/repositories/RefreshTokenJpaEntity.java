package com.sems.iam.infrastructure.persistence.jpa.repositories;

import jakarta.persistence.*;
import java.time.Instant;
import java.util.UUID;
import lombok.*;

/**
 * Token de refresco emitido a un usuario.
 *
 * <p><b>Se guarda el resumen SHA-256, nunca el token en claro.</b> Si alguien
 * obtuviera una copia de esta tabla no podria suplantar a nadie: el valor
 * almacenado no sirve para autenticarse.
 */
@Entity
@Table(name = "iam_refresh_tokens",
        indexes = {
                @Index(name = "idx_iam_refresh_user", columnList = "user_id"),
                @Index(name = "idx_iam_refresh_hash", columnList = "token_hash", unique = true)
        })
@Getter @Setter @NoArgsConstructor @AllArgsConstructor
public class RefreshTokenJpaEntity {

    @Id
    @Column(name = "id", nullable = false, updatable = false)
    private UUID id;

    @Column(name = "user_id", nullable = false)
    private UUID userId;

    @Column(name = "token_hash", nullable = false, length = 128)
    private String tokenHash;

    @Column(name = "expires_at", nullable = false)
    private Instant expiresAt;

    @Column(name = "revoked", nullable = false)
    private boolean revoked;

    @Column(name = "created_at", nullable = false)
    private Instant createdAt;
}
