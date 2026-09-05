package com.sems.iam;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.*;

import com.sems.iam.application.internal.commandservices.AuthTokenService;
import com.sems.iam.domain.model.exceptions.UnauthorizedException;
import com.sems.iam.infrastructure.persistence.jpa.repositories.*;
import java.time.Instant;
import java.time.temporal.ChronoUnit;
import java.util.HashMap;
import java.util.Map;
import java.util.Optional;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.test.util.ReflectionTestUtils;

/**
 * El manejo de tokens es la parte del sistema donde un fallo silencioso tiene
 * peores consecuencias, asi que las garantias se fijan con pruebas.
 */
class AuthTokenServiceTest {

    private final Map<String, RefreshTokenJpaEntity> refreshStore = new HashMap<>();
    private final Map<String, UserAuthTokenJpaEntity> authStore = new HashMap<>();

    private RefreshTokenRepository refreshTokens;
    private UserAuthTokenRepository authTokens;
    private AuthTokenService service;

    @BeforeEach
    void setUp() {
        refreshStore.clear();
        authStore.clear();

        refreshTokens = mock(RefreshTokenRepository.class);
        authTokens = mock(UserAuthTokenRepository.class);

        when(refreshTokens.save(any())).thenAnswer(inv -> {
            RefreshTokenJpaEntity e = inv.getArgument(0);
            refreshStore.put(e.getTokenHash(), e);
            return e;
        });
        when(refreshTokens.findByTokenHash(anyString()))
                .thenAnswer(inv -> Optional.ofNullable(refreshStore.get(inv.getArgument(0))));

        when(authTokens.save(any())).thenAnswer(inv -> {
            UserAuthTokenJpaEntity e = inv.getArgument(0);
            authStore.put(e.getTokenHash() + "|" + e.getPurpose(), e);
            return e;
        });
        when(authTokens.findByTokenHashAndPurpose(anyString(), anyString()))
                .thenAnswer(inv -> Optional.ofNullable(
                        authStore.get(inv.getArgument(0) + "|" + inv.getArgument(1))));

        service = new AuthTokenService(refreshTokens, authTokens);
        ReflectionTestUtils.setField(service, "refreshExpirationDays", 30);
    }

    @Test
    @DisplayName("El token nunca se guarda en claro, solo su resumen")
    void tokenIsStoredHashed() {
        UUID userId = UUID.randomUUID();

        String raw = service.issueRefreshToken(userId);

        assertFalse(refreshStore.containsKey(raw), "el valor en claro no debe ser la clave");
        RefreshTokenJpaEntity stored = refreshStore.values().iterator().next();
        assertNotEquals(raw, stored.getTokenHash(), "se debe guardar el resumen, no el token");
        assertEquals(64, stored.getTokenHash().length(), "SHA-256 en hexadecimal son 64 caracteres");
    }

    @Test
    @DisplayName("Dos emisiones seguidas producen tokens distintos")
    void tokensAreUnique() {
        UUID userId = UUID.randomUUID();
        assertNotEquals(service.issueRefreshToken(userId), service.issueRefreshToken(userId));
    }

    @Test
    @DisplayName("Usar el token de refresco lo rota: el anterior deja de valer")
    void refreshTokenRotates() {
        UUID userId = UUID.randomUUID();
        String raw = service.issueRefreshToken(userId);

        assertEquals(userId, service.consumeRefreshToken(raw));
        assertThrows(UnauthorizedException.class, () -> service.consumeRefreshToken(raw),
                "reutilizar un token ya consumido debe fallar");
    }

    @Test
    @DisplayName("Un token de refresco desconocido o vencido se rechaza")
    void invalidRefreshTokensAreRejected() {
        assertThrows(UnauthorizedException.class, () -> service.consumeRefreshToken("inventado"));

        UUID userId = UUID.randomUUID();
        String raw = service.issueRefreshToken(userId);
        refreshStore.values().forEach(e -> e.setExpiresAt(Instant.now().minus(1, ChronoUnit.DAYS)));

        assertThrows(UnauthorizedException.class, () -> service.consumeRefreshToken(raw));
    }

    @Test
    @DisplayName("Un enlace de recuperacion solo sirve una vez")
    void singleUseTokenCannotBeReused() {
        UUID userId = UUID.randomUUID();
        String raw = service.issuePasswordResetToken(userId);

        assertEquals(userId, service.consumeSingleUse(raw,
                UserAuthTokenJpaEntity.PURPOSE_PASSWORD_RESET));
        assertThrows(UnauthorizedException.class, () -> service.consumeSingleUse(raw,
                UserAuthTokenJpaEntity.PURPOSE_PASSWORD_RESET));
    }

    @Test
    @DisplayName("Un token de verificacion no sirve para cambiar la contrasena")
    void purposesAreNotInterchangeable() {
        UUID userId = UUID.randomUUID();
        String verification = service.issueVerificationToken(userId);

        assertThrows(UnauthorizedException.class, () -> service.consumeSingleUse(verification,
                UserAuthTokenJpaEntity.PURPOSE_PASSWORD_RESET),
                "un token emitido para verificar no debe valer para recuperar");
    }
}
