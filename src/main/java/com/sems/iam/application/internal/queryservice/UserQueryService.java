package com.sems.iam.application.internal.queryservice;

import com.sems.iam.infrastructure.persistence.jpa.repositories.*;
import com.sems.iam.domain.model.exceptions.NotFoundException;
import com.sems.iam.interfaces.rest.resources.UserResource;
import java.util.*;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;

/**
 * UserQueryService is the "query" side (read-only) of the CQRS split: commands
 * change data, queries only fetch it. Notice how short these methods are -
 * reads usually have no business rules, so the service just loads data and turns
 * it into a response resource (DTO).
 */
@Service
@RequiredArgsConstructor
public class UserQueryService {
    private final UserRepository userRepository;
    private final UserRoleRepository userRoleRepository;

    /** Fetches one user by id, or throws 404-style NotFoundException. */
    public UserResource getById(UUID userId) {
        UserJpaEntity user = userRepository.findById(userId).orElseThrow(() -> new NotFoundException("User not found"));
        return toResource(user);
    }

    /** Fetches every user and maps each one to a resource. */
    public List<UserResource> getAll() { return userRepository.findAll().stream().map(this::toResource).toList(); }

    /**
     * Private helper that converts a database entity into the API resource the
     * outside world sees. It also loads the user's role names. Keeping this
     * mapping in one place avoids repeating it in getById and getAll.
     */
    private UserResource toResource(UserJpaEntity user) {
        List<String> roles = userRoleRepository.findByUser_UserId(user.getUserId()).stream().map(ur -> ur.getRole().getName()).toList();
        return new UserResource(user.getUserId(), user.getEmailAddress(), roles);
    }
}
