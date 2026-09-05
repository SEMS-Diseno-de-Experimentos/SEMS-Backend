package com.sems.iam.application.internal.commandservices;

import com.sems.iam.application.internal.outboundservices.IamEventPublisher;
import com.sems.iam.domain.model.aggregates.UserAggregate;
import com.sems.iam.domain.model.exceptions.NotFoundException;
import com.sems.iam.domain.model.exceptions.UnauthorizedException;
import com.sems.iam.domain.model.valueobjects.EmailAddress;
import com.sems.iam.domain.model.valueobjects.RoleName;
import com.sems.iam.domain.services.PasswordHashingService;
import com.sems.iam.domain.services.TokenService;
import com.sems.iam.infrastructure.persistence.jpa.repositories.*;
import com.sems.iam.interfaces.rest.resources.LoginResponse;
import java.time.Instant;
import java.util.List;
import java.util.UUID;
import java.util.stream.Collectors;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * Verificacion de cuenta, recuperacion de contrasena, refresco y cierre de sesion.
 *
 * <p>Se separa de {@code AuthenticationCommandService} para que ese no siga
 * creciendo: alli viven el alta y el inicio de sesion; aqui, el ciclo de vida
 * de la credencial.
 */
@Slf4j
@Service
@RequiredArgsConstructor
public class AccountRecoveryService {

    public static final String STATUS_PENDING = "PENDING";
    public static final String STATUS_ACTIVE = "ACTIVE";

    private final UserRepository userRepository;
    private final UserRoleRepository userRoleRepository;
    private final PasswordHashingService passwordHashingService;
    private final TokenService tokenService;
    private final AuthTokenService authTokens;
    private final IamEventPublisher eventPublisher;

    /**
     * Entrega un par de tokens nuevo a partir de uno de refresco valido.
     *
     * <p>El de refresco se rota en el proceso: el anterior queda revocado.
     */
    @Transactional
    public LoginResponse refresh(String refreshToken) {
        UUID userId = authTokens.consumeRefreshToken(refreshToken);
        UserJpaEntity user = userRepository.findById(userId)
                .orElseThrow(() -> new UnauthorizedException("Invalid refresh token"));
        return buildSession(user);
    }

    /** Cierra la sesion. Sin token concreto, cierra todas las del usuario. */
    @Transactional
    public void logout(UUID userId, String refreshToken) {
        authTokens.revoke(userId, refreshToken);
    }

    /** Solicita el envio del codigo de verificacion de cuenta. */
    @Transactional
    public void requestVerification(UUID userId) {
        UserJpaEntity user = userRepository.findById(userId)
                .orElseThrow(() -> new NotFoundException("User not found"));
        String token = authTokens.issueVerificationToken(user.getUserId());
        eventPublisher.publishVerificationRequested(user.getUserId().toString(),
                user.getEmailAddress(), token);
    }

    /** Activa la cuenta y devuelve una sesion, para que el usuario entre directo. */
    @Transactional
    public LoginResponse verifyAccount(String token) {
        UUID userId = authTokens.consumeSingleUse(token,
                UserAuthTokenJpaEntity.PURPOSE_VERIFICATION);

        UserJpaEntity user = userRepository.findById(userId)
                .orElseThrow(() -> new NotFoundException("User not found"));
        user.setStatus(STATUS_ACTIVE);
        user.setUpdatedAt(Instant.now());
        userRepository.save(user);

        return buildSession(user);
    }

    /**
     * Inicia la recuperacion de contrasena.
     *
     * <p><b>No revela si el correo existe.</b> El metodo termina en silencio
     * cuando no hay cuenta asociada, y el controlador responde siempre lo mismo.
     * Contestar distinto convertiria este endpoint en un verificador de correos
     * registrados para cualquiera que lo consulte.
     */
    @Transactional
    public void forgotPassword(String emailAddress) {
        EmailAddress email = new EmailAddress(emailAddress);
        userRepository.findByEmailAddress(email.value()).ifPresentOrElse(user -> {
            String token = authTokens.issuePasswordResetToken(user.getUserId());
            eventPublisher.publishPasswordResetRequested(user.getUserId().toString(),
                    user.getEmailAddress(), token);
        }, () -> log.info("Recuperacion solicitada para un correo no registrado; no se envia nada"));
    }

    /**
     * Cambia la contrasena y cierra todas las sesiones abiertas.
     *
     * <p>Revocar los tokens es parte del caso de uso: si el usuario cambia la
     * contrasena porque sospecha que alguien entro, dejar viva la sesion del
     * intruso vaciaria de sentido la operacion.
     */
    @Transactional
    public void resetPassword(String token, String newPassword) {
        UUID userId = authTokens.consumeSingleUse(token,
                UserAuthTokenJpaEntity.PURPOSE_PASSWORD_RESET);

        UserJpaEntity user = userRepository.findById(userId)
                .orElseThrow(() -> new NotFoundException("User not found"));
        user.setPasswordHash(passwordHashingService.hash(newPassword));
        user.setUpdatedAt(Instant.now());
        userRepository.save(user);

        authTokens.revoke(userId, null);
    }

    /** Arma el par de tokens y la respuesta de sesion para un usuario. */
    private LoginResponse buildSession(UserJpaEntity user) {
        List<String> roles = userRoleRepository.findByUser_UserId(user.getUserId()).stream()
                .map(ur -> ur.getRole().getName()).toList();

        UserAggregate aggregate = new UserAggregate(user.getUserId(),
                new EmailAddress(user.getEmailAddress()), user.getPasswordHash(),
                roles.stream().map(RoleName::from).collect(Collectors.toSet()),
                user.getCreatedAt(), user.getUpdatedAt());

        String accessToken = tokenService.generateToken(aggregate);
        String refreshToken = authTokens.issueRefreshToken(user.getUserId());

        return new LoginResponse(accessToken, refreshToken, user.getUserId(),
                user.getEmailAddress(), roles);
    }
}
