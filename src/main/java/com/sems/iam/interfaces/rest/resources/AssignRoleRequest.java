package com.sems.iam.interfaces.rest.resources;
import jakarta.validation.constraints.NotBlank;
public record AssignRoleRequest(@NotBlank String role) {}
