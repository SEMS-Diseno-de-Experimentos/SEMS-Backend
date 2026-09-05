package com.sems.iam.interfaces.rest.resources;
import jakarta.validation.constraints.*;
public record RegisterRequest(@NotBlank @Email String emailAddress, @NotBlank @Size(min = 6, max = 100) String password, @NotBlank String role) {}
