# Organizations Mutation Admission Policy Task

Status: completed
Date: 2026-07-31

## Goal

Let a composed product reject or temporarily defer owner-authorized organization
and join-source mutations without teaching Organizations about tenant
termination, billing, employment, or product workflow state.

## Delivery Slice

1. Add a Contracts-only mutation operation, context, decision, and policy
   interface.
2. Preserve current behavior when no policy is registered.
3. Evaluate registered policies after owner authorization and request/resource
   validation, but before the first aggregate mutation or token issuance.
4. Compose policies fail-closed: denial returns a stable conflict, while an
   unknown result, unavailable dependency, or policy failure returns a stable
   service-unavailable error.
5. Cover organization profile, lifecycle, ownership transfer, invitation
   issuance/reissue, enrollment-link issuance/rotation, and non-idempotent
   trusted membership restoration.
6. Keep invitation revocation, enrollment-link disablement, membership
   suspension/removal, and administration recovery outside this policy.

## Ownership Boundaries

Organizations owns the generic pre-mutation seam because it owns the affected
commands and aggregate invariants. Composed products own policy implementations
and their authoritative decision state.

The contract carries only an organization id, operation, actor subject id, and
optional target identifiers. Product names, tenant lifecycle vocabulary, roles,
staff records, and authorization profiles must not enter this module.

Exact idempotent issuance and active-membership replays return current state
without evaluating mutation admission because they do not mutate state or
replay a plaintext token.

## Acceptance Criteria

- existing hosts without a policy retain current behavior;
- every covered state-expanding owner or trusted lifecycle mutation is
  admitted before its first write;
- denied or unavailable decisions leave aggregates and join-source collections
  unchanged;
- source revocation and disablement remain available for defensive cleanup;
- public errors map to stable `409` and `503` responses;
- contract enums use stable string wire names and reject unknown values;
- no reusable module dependency is added.

## Verification

- focused contract, endpoint, policy, organization, invitation, and enrollment
  tests passed;
- the standalone non-Docker module verification gate passed with a zero-warning
  build, 154 unit tests, both provider migration-drift checks, and no vulnerable
  packages;
- no persistence migration is required because this slice adds no owned state.
