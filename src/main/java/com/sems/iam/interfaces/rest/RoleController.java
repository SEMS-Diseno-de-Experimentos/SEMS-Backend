package com.sems.iam.interfaces.rest;

import com.sems.iam.application.internal.commandservices.UserRoleCommandService;
import com.sems.iam.interfaces.rest.resources.AssignRoleRequest;
import com.sems.iam.interfaces.rest.transform.CommandMapper;
import jakarta.validation.Valid;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.*;

@RestController
@RequestMapping("/api/v1/users")
@RequiredArgsConstructor
public class RoleController {
    private final UserRoleCommandService userRoleCommandService;
    private final CommandMapper commandMapper;

    @PreAuthorize("hasRole('ADMIN')")
    @PostMapping("/{userId}/roles")
    public ResponseEntity<Void> assignRole(@PathVariable UUID userId, @Valid @RequestBody AssignRoleRequest request) {
        userRoleCommandService.assignRole(commandMapper.toCommand(userId, request));
        return ResponseEntity.ok().build();
    }
}
