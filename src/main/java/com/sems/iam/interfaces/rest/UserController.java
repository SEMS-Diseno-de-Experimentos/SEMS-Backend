package com.sems.iam.interfaces.rest;

import com.sems.iam.application.internal.queryservice.UserQueryService;
import com.sems.iam.infrastructure.authorization.sfs.model.AuthenticatedUserDetails;
import com.sems.iam.interfaces.rest.resources.UserResource;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.*;

@RestController
@RequestMapping("/api/v1/users")
@RequiredArgsConstructor
public class UserController {
    private final UserQueryService userQueryService;

    @GetMapping("/me")
    public ResponseEntity<UserResource> me(@AuthenticationPrincipal AuthenticatedUserDetails user) { return ResponseEntity.ok(userQueryService.getById(user.getUserId())); }

    @PreAuthorize("hasRole('ADMIN')")
    @GetMapping
    public ResponseEntity<List<UserResource>> all() { return ResponseEntity.ok(userQueryService.getAll()); }
}
