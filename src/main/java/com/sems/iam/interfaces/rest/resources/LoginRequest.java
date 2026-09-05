package com.sems.iam.interfaces.rest.resources;
import jakarta.validation.constraints.*;
public record LoginRequest(@NotBlank @Email String emailAddress, @NotBlank String password) {}
