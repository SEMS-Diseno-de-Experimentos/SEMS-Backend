package com.sems.iam.infrastructure.persistence.jpa.repositories;

import jakarta.persistence.*;
import java.time.Instant;
import java.util.UUID;
import lombok.*;

/**
 * Token de un solo uso: verificacion de cuenta o recuperacion de contrasena.
 *
 * <p>Como los de refresco, se guarda el resumen y no el valor en claro. La
 * columna {@code used_at} garantiza que un enlace no pueda reutilizarse aunque
 * el correo quede archivado o se reenvie.
 */
@Entity
@Table(name = "iam_user_auth_tokens",
        indexes = {
                @Index(name = "idx_iam_authtoken_user", columnList = "user_id"),
                @Index(name = "idx_iam_authtoken_hash", columnList = "token_hash", unique = true)
        })
@Getter @Setter @NoArgsConstructor @AllArgsConstructor
public class UserAuthTokenJpaEntity {

    public static final String PURPOSE_VERIFICATION = "VERIFICATION";
    public static final String PURPOSE_PASSWORD_RESET = "PASSWORD_RESET";

    @Id
    @Column(name = "id", nullable = false, updatable = false)
    private UUID id;

    @Column(name = "user_id", nullable = false)
    private UUID userId;

    @Column(name = "token_hash", nullable = false, length = 128)
    private String tokenHash;

    @Column(name = "purpose", nullable = false, length = 40)
    private String purpose;

    @Column(name = "expires_at", nullable = false)
    private Instant expiresAt;

    @Column(name = "used_at")
    private Instant usedAt;

    @Column(name = "created_at", nullable = false)
    private Instant createdAt;
}
