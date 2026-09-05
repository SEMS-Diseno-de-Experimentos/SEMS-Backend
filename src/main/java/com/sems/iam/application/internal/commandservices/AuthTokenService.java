package com.sems.iam.application.internal.commandservices;

import com.sems.iam.domain.model.exceptions.UnauthorizedException;
import com.sems.iam.infrastructure.persistence.jpa.repositories.*;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.SecureRandom;
import java.time.Duration;
import java.time.Instant;
import java.util.Base64;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * Emision y consumo de tokens opacos: refresco, verificacion y recuperacion.
 *
 * <p>Tres decisiones de seguridad que conviene no perder de vista:
 * <ol>
 *   <li><b>Se guarda el resumen SHA-256, nunca el token.</b> Una copia de la
 *       base de datos no permite suplantar a nadie.</li>
 *   <li><b>El valor se genera con {@link SecureRandom}</b>, no con
 *       {@code Math.random} ni con un UUID: 32 bytes de entropia real.</li>
 *   <li><b>Los tokens de un solo uso se marcan como usados.</b> Un enlace de
 *       recuperacion reenviado o archivado deja de servir tras el primer uso.</li>
 * </ol>
 */
@Service
@RequiredArgsConstructor
public class AuthTokenService {

    /** Ventana de validez del enlace de recuperacion de contrasena. */
    private static final Duration RESET_TTL = Duration.ofHours(1);

    /** Ventana de validez del codigo de verificacion de cuenta. */
    private static final Duration VERIFICATION_TTL = Duration.ofHours(24);

    private static final SecureRandom RANDOM = new SecureRandom();

    private final RefreshTokenRepository refreshTokens;
    private final UserAuthTokenRepository authTokens;

    @Value("${security.jwt.refresh-expiration-days:30}")
    private int refreshExpirationDays;

    /**
     * Emite un token de refresco y devuelve su valor en claro.
     *
     * <p>Es la unica vez que ese valor existe fuera del navegador del usuario:
     * en base de datos solo queda el resumen.
     */
    @Transactional
    public String issueRefreshToken(UUID userId) {
        String raw = randomToken();
        RefreshTokenJpaEntity entity = new RefreshTokenJpaEntity(UUID.randomUUID(), userId,
                sha256(raw), Instant.now().plus(Duration.ofDays(refreshExpirationDays)),
                false, Instant.now());
        refreshTokens.save(entity);
        return raw;
    }

    /**
     * Valida un token de refresco y lo rota.
     *
     * <p>La rotacion es deliberada: el token usado se revoca y se entrega uno
     * nuevo. Si alguien robara uno y lo usara, el legitimo dejaria de funcionar
     * y el robo se haria visible.
     *
     * @return el identificador del usuario dueno del token
     */
    @Transactional
    public UUID consumeRefreshToken(String rawToken) {
        RefreshTokenJpaEntity stored = refreshTokens.findByTokenHash(sha256(rawToken))
                .orElseThrow(() -> new UnauthorizedException("Invalid refresh token"));

        if (stored.isRevoked()) {
            throw new UnauthorizedException("Refresh token was revoked");
        }
        if (stored.getExpiresAt().isBefore(Instant.now())) {
            throw new UnauthorizedException("Refresh token expired");
        }

        stored.setRevoked(true);
        refreshTokens.save(stored);
        return stored.getUserId();
    }

    /** Revoca un token concreto; si no se indica, cierra todas las sesiones. */
    @Transactional
    public void revoke(UUID userId, String rawToken) {
        if (rawToken != null && !rawToken.isBlank()) {
            refreshTokens.findByTokenHash(sha256(rawToken)).ifPresent(stored -> {
                stored.setRevoked(true);
                refreshTokens.save(stored);
            });
            return;
        }
        if (userId != null) {
            refreshTokens.revokeAllForUser(userId);
        }
    }

    @Transactional
    public String issueVerificationToken(UUID userId) {
        return issueSingleUse(userId, UserAuthTokenJpaEntity.PURPOSE_VERIFICATION, VERIFICATION_TTL);
    }

    @Transactional
    public String issuePasswordResetToken(UUID userId) {
        return issueSingleUse(userId, UserAuthTokenJpaEntity.PURPOSE_PASSWORD_RESET, RESET_TTL);
    }

    /**
     * Valida y marca como usado un token de un solo uso.
     *
     * @return el identificador del usuario dueno del token
     */
    @Transactional
    public UUID consumeSingleUse(String rawToken, String purpose) {
        UserAuthTokenJpaEntity stored = authTokens
                .findByTokenHashAndPurpose(sha256(rawToken), purpose)
                .orElseThrow(() -> new UnauthorizedException("Invalid or unknown token"));

        if (stored.getUsedAt() != null) {
            throw new UnauthorizedException("Token was already used");
        }
        if (stored.getExpiresAt().isBefore(Instant.now())) {
            throw new UnauthorizedException("Token expired");
        }

        stored.setUsedAt(Instant.now());
        authTokens.save(stored);
        return stored.getUserId();
    }

    private String issueSingleUse(UUID userId, String purpose, Duration ttl) {
        String raw = randomToken();
        authTokens.save(new UserAuthTokenJpaEntity(UUID.randomUUID(), userId, sha256(raw),
                purpose, Instant.now().plus(ttl), null, Instant.now()));
        return raw;
    }

    /** 32 bytes de entropia en base64 apta para URL. */
    private static String randomToken() {
        byte[] bytes = new byte[32];
        RANDOM.nextBytes(bytes);
        return Base64.getUrlEncoder().withoutPadding().encodeToString(bytes);
    }

    private static String sha256(String value) {
        try {
            MessageDigest digest = MessageDigest.getInstance("SHA-256");
            byte[] hash = digest.digest(value.getBytes(StandardCharsets.UTF_8));
            StringBuilder hex = new StringBuilder(hash.length * 2);
            for (byte b : hash) {
                hex.append(String.format("%02x", b));
            }
            return hex.toString();
        } catch (Exception e) {
            throw new IllegalStateException("SHA-256 no disponible", e);
        }
    }
}
