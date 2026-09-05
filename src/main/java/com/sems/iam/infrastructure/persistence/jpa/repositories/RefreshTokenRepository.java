package com.sems.iam.infrastructure.persistence.jpa.repositories;

import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface RefreshTokenRepository extends JpaRepository<RefreshTokenJpaEntity, UUID> {

    Optional<RefreshTokenJpaEntity> findByTokenHash(String tokenHash);

    /** Cierra la sesion en todos los dispositivos del usuario. */
    @Modifying
    @Query("update RefreshTokenJpaEntity t set t.revoked = true where t.userId = :userId")
    int revokeAllForUser(@Param("userId") UUID userId);
}
