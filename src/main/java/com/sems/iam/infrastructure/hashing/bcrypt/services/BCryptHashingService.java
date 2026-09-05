package com.sems.iam.infrastructure.hashing.bcrypt.services;

import com.sems.iam.domain.services.PasswordHashingService;
import org.springframework.security.crypto.bcrypt.BCryptPasswordEncoder;
import org.springframework.stereotype.Service;

@Service
public class BCryptHashingService implements PasswordHashingService {
    private final BCryptPasswordEncoder encoder = new BCryptPasswordEncoder();

    @Override public String hash(String rawPassword) { return encoder.encode(rawPassword); }
    @Override public boolean matches(String rawPassword, String hashedPassword) { return encoder.matches(rawPassword, hashedPassword); }
}
