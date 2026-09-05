package com.sems.iam.domain.model.commands;

import java.util.UUID;

public record AssignRoleCommand(UUID userId, String role) {
}
