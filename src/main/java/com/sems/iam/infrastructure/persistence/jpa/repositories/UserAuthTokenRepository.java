package com.sems.iam.infrastructure.persistence.jpa.repositories;

import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

public interface UserAuthTokenRepository extends JpaRepository<UserAuthTokenJpaEntity, UUID> {
    Optional<UserAuthTokenJpaEntity> findByTokenHashAndPurpose(String tokenHash, String purpose);
}
