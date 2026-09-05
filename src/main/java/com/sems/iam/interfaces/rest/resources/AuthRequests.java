package com.sems.iam.interfaces.rest.resources;

import jakarta.validation.constraints.Email;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

/** Cuerpos de las operaciones de autenticacion anadidas. */
public final class AuthRequests {

    private AuthRequests() {
    }

    public record RefreshRequest(
            @NotBlank(message = "is required") String refreshToken) {
    }

    public record LogoutRequest(String refreshToken) {
    }

    public record VerifyRequest(
            @NotBlank(message = "is required") String token) {
    }

    public record ForgotPasswordRequest(
            @NotBlank(message = "is required") @Email(message = "must be a valid email") String emailAddress) {
    }

    public record ResetPasswordRequest(
            @NotBlank(message = "is required") String token,
            @NotBlank(message = "is required")
            @Size(min = 8, message = "must be at least 8 characters") String newPassword) {
    }
}
