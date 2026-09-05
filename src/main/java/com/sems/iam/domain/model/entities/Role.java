package com.sems.iam.domain.model.entities;

import com.sems.iam.domain.model.valueobjects.RoleName;
import java.util.UUID;

/**
 * Role is a domain entity: unlike a value object, it has its own identity (the
 * roleId), so two roles are "the same" when their ids match, even if other
 * fields differ.
 *
 * It pairs a unique id with a RoleName value object (an enum-like type that only
 * allows valid role names). Using a record keeps this immutable and concise:
 * a Role, once created, never changes.
 */
public record Role(UUID roleId, RoleName name) {
}
