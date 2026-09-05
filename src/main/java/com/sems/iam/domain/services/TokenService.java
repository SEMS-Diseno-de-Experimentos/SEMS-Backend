package com.sems.iam.domain.services;

import com.sems.iam.domain.model.aggregates.UserAggregate;

/**
 * TokenService is a "port" in the ports-and-adapters (hexagonal) architecture.
 * It is an interface owned by the domain that declares WHAT we need from a token
 * mechanism, without saying HOW it is implemented. The concrete adapter lives in
 * the infrastructure layer (see JwtService, which implements this with JWTs).
 *
 * Thanks to this interface the domain/application code depends only on the
 * abstraction, so we could swap JWT for another token technology, or provide a
 * fake implementation in tests, without changing the business logic.
 */
public interface TokenService {
    /** Builds a signed token that represents the given user. */
    String generateToken(UserAggregate userAggregate);

    /** Reads the user id stored inside a token. */
    String extractUserId(String token);

    /** Returns true if the token is genuine and not expired/tampered with. */
    boolean isTokenValid(String token);
}
