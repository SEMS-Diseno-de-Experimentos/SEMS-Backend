package com.sems.iam.infrastructure.persistence.jpa.repositories;

import java.util.List;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

public interface UserRoleRepository extends JpaRepository<UserRoleJpaEntity, UserRoleId> {
    List<UserRoleJpaEntity> findByUser_UserId(UUID userId);
    boolean existsById(UserRoleId id);
}
