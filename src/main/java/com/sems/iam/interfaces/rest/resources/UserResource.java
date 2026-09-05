package com.sems.iam.interfaces.rest.resources;
import java.util.*;
public record UserResource(UUID userId, String emailAddress, List<String> roles) {}
