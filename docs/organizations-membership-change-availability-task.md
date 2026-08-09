# Organizations Membership-Change Availability Task

Status: in progress
Date: 2026-08-09

## Goal

Make owner-facing membership-change policies distinguish a deliberate product
denial from temporarily unavailable or invalid policy state without changing
Organizations membership ownership or BunkFy Staff workflow semantics.

## Audit Finding

`IOrganizationMembershipChangePolicy` returns `Allowed`, `Denied`, or the
invalid `Unknown` sentinel, but the command handler evaluates policies directly.
Every non-allowed value is reported as the permanent
`Organizations.MembershipChangeRejected` conflict, while a policy exception
escapes the module boundary. A transient product projection or dependency can
therefore become either a misleading HTTP 409 or an unclassified HTTP 500.

This differs from the mature Organizations mutation and join-admission policy
pipelines. Membership changes need the same fail-closed distinction before the
first owner-count or membership mutation.

## Decision Contract

1. Add `Unavailable` to the Contracts decision enum with the stable
   `"unavailable"` wire name. `Unknown` remains an invalid sentinel.
2. No installed policies preserves standalone Organizations behavior and allows
   the change. Every installed policy must return `Allowed`.
3. `Denied` returns the existing
   `Organizations.MembershipChangeRejected` error and HTTP 409.
4. `Unavailable`, `Unknown`, an undefined enum value, or a non-cancellation
   policy exception returns a new stable
   `Organizations.MembershipChangeUnavailable` error and HTTP 503.
5. Caller-requested cancellation propagates instead of being converted to an
   availability failure. Policy evaluation stops after the first failure.
6. Authorization, resource lookup, exact replay, and optimistic-version checks
   retain their current order. Fresh policy evaluation occurs after those
   checks and before the first aggregate mutation.
7. A failed policy leaves organization and membership versions, state, and
   domain-event collections unchanged. Exact committed retries continue to
   bypass product policy and return their bounded replay result.

## Ownership

- Organizations Contracts owns the provider-neutral decision value.
- Organizations Application owns composition, exception containment, stable
  errors, logging, and mutation placement; Organizations API owns HTTP mapping.
- BunkFy Workspaces keeps its existing unconditional denial so Staff's durable
  lifecycle process remains the only product path for ordinary membership
  suspension, resumption, or removal.
- GMA Framework needs no change. Decision vocabulary, replay placement, error
  mapping, and policy logs are module behavior rather than generic CQRS
  infrastructure.

## Delivery

- [x] Add strict `Unavailable` serialization and contract coverage.
- [x] Add one scoped Organizations policy composer with denial, availability,
  exception, cancellation, and short-circuit tests.
- [x] Route owner-facing membership changes through the composer without
  changing authorization, replay, concurrency, or aggregate behavior.
- [x] Add stable API status coverage and adversarial no-mutation tests.
- [x] Verify the unchanged BunkFy deny-only policy consumer.
- [ ] Run one consolidated non-Docker gate per changed repository at the
  completed slice boundary, publish exact pins, and verify CI.

## Verification Plan

- Use focused Organizations contract, policy, handler, and API tests while
  editing, plus the focused BunkFy Workspaces policy test.
- Run no Docker/provider gate because this slice changes no persistence model,
  query, migration, transaction boundary, or provider behavior.
- At the completed slice boundary, run the full Organizations non-Docker gate,
  the BunkFy backend non-Docker consumer gate, and Skeleton source/architecture
  verification once before publication.

## Verification Evidence

- Focused Organizations contract, policy, handler, and API coverage passed:
  89 tests.
- The unchanged BunkFy Workspaces membership-change policy consumer passed:
  1 test.
- `pwsh eng/verify.ps1 -SkipDocker` passed in Organizations, including source
  and boundary guards, a zero-warning build, PostgreSQL and SQL Server migration
  drift checks, 277 fast tests, and the vulnerability scan.
- `pwsh eng/verify.ps1 -SkipRestore` passed in the BunkFy backend, including
  source and architecture guards, a zero-warning build, migration drift checks,
  all fast suites, and 54 integration tests.
- Skeleton verification and exact-pin publication remain pending.

## Not In This Slice

- changing Staff lifecycle orchestration or allowing direct membership changes
  in BunkFy;
- changing membership roles, owner protection, access-profile assignments, or
  trusted lifecycle facades;
- retrying, caching, or persisting product policy decisions;
- changing invitation, enrollment, organization-mutation, or join-source
  policy contracts;
- changing persistence schemas or migrations; or
- introducing a generic Framework policy pipeline.
