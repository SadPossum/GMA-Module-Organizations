# Organizations Membership Change Policy Task

Status: completed
Date: 2026-07-21

## Goal

Let a host or product reject owner-initiated ordinary membership lifecycle changes before Organizations mutates its aggregates, without teaching Organizations about product employment, access profiles, or workflow state.

## Delivery Slice

1. Add a Contracts-only membership-change policy request, decision, and interface.
2. Keep Organizations permissive when no policy is registered.
3. Evaluate every registered policy after owner authorization and entity lookup, but before organization or membership state changes.
4. Return one stable Organizations error when any policy denies the request.
5. Keep the trusted, idempotent `IOrganizationMembershipLifecycle` facade independent from owner-facing product governance policies.
6. Prove optional and denied behavior with focused application tests and the complete module verification gate.

## Ownership Boundaries

Organizations owns the generic pre-mutation extension seam because it owns membership lifecycle commands. A product owns each policy implementation and may require its own workflow to be used instead.

Policies receive only organization and membership contract data. They must not introduce product role names, staff records, property assignments, or authorization-profile concepts into this module.

## Acceptance Criteria

- existing consumers without a policy retain current behavior;
- one denial prevents both organization owner-count and membership mutations;
- multiple policies compose deny-first;
- lifecycle-facade behavior and owner protection remain unchanged;
- no reusable module dependency is added;
- the standalone module verification gate passes on Windows and Linux.

## Verification

- Organizations boundary and migration-drift checks passed for PostgreSQL and SQL Server.
- The solution built with zero warnings.
- 57 unit tests and 4 integration tests passed.
- NuGet vulnerability checks reported no vulnerable packages.
