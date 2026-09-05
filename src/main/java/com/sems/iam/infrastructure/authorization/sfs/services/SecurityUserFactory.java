package com.sems.iam.infrastructure.authorization.sfs.services;

import com.sems.iam.infrastructure.authorization.sfs.model.AuthenticatedUserDetails;
import java.util.List;
import java.util.UUID;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.stereotype.Service;

@Service
public class SecurityUserFactory {
    public AuthenticatedUserDetails create(UUID userId, String email, List<String> roles) {
        var authorities = roles.stream().map(r -> new SimpleGrantedAuthority("ROLE_" + r)).toList();
        return new AuthenticatedUserDetails(userId, email, authorities);
    }
}
