package com.sems.iam.application.internal.commandservices;

import com.sems.iam.application.internal.outboundservices.IamEventPublisher;
import com.sems.iam.domain.model.exceptions.NotFoundException;
import com.sems.iam.domain.model.commands.AssignRoleCommand;
import com.sems.iam.domain.model.valueobjects.RoleName;
import com.sems.iam.infrastructure.persistence.jpa.repositories.*;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * UserRoleCommandService contains the use case for giving a user a new role.
 * Like the other command services it lives in the application layer and just
 * coordinates the repositories and the event publisher.
 */
@Service
@RequiredArgsConstructor
public class UserRoleCommandService {
    private final UserRepository userRepository;
    private final RoleRepository roleRepository;
    private final UserRoleRepository userRoleRepository;
    private final IamEventPublisher eventPublisher;

    /**
     * Assigns a role to a user. The whole method runs in one transaction so the
     * save and the event publishing succeed or fail together.
     */
    @Transactional
    public void assignRole(AssignRoleCommand command) {
        // Make sure both the user and the role actually exist before linking.
        var user = userRepository.findById(command.userId()).orElseThrow(() -> new NotFoundException("User not found"));
        RoleName roleName = RoleName.from(command.role());
        var role = roleRepository.findByName(roleName.name()).orElseThrow(() -> new NotFoundException("Role not found"));
        // The link is identified by the (userId, roleId) pair (a composite key).
        UserRoleId id = new UserRoleId(user.getUserId(), role.getRoleId());
        // This is an "idempotent" guard: if the user already has the role we do
        // nothing, so calling the endpoint twice cannot create duplicates or
        // fire the event a second time.
        if (!userRoleRepository.existsById(id)) {
            UserRoleJpaEntity join = new UserRoleJpaEntity();
            join.setId(id); join.setUser(user); join.setRole(role);
            userRoleRepository.save(join);
            eventPublisher.publishRoleAssigned(user.getUserId().toString(), roleName.name());
        }
    }
}
