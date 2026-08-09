# Organizations Join-Admission Availability Task

Status: complete
Date: 2026-08-09

## Goal

Make product-owned admission decisions for invitation acceptance, enrollment
claims, and join-request approval distinguish a deliberate business denial from
a temporarily unavailable dependency.

## Audit Finding

`IOrganizationJoinAdmissionPolicy` currently returns `bool`. A product can say
only allowed or not allowed, so a failed or indeterminate product projection is
reported as `Organizations.JoinAdmissionRejected` and HTTP 409. BunkFy exposes
this loss directly: its workspace operational gate has `Allowed`, `Restricted`,
and `Unavailable`, but both non-allowed outcomes become `false`.

This is operationally dishonest. A restricted workspace should reject the
join, while unavailable authoritative state should fail closed with a retryable
service response and no source capacity, membership, or claim mutation.

## Decision Contract

1. Organizations Contracts owns a join-admission decision with `Allowed`,
   `Denied`, and `Unavailable` outcomes plus an invalid `Unknown` sentinel.
2. No installed policies preserves the current standalone-module behavior and
   allows admission.
3. Every installed policy must return `Allowed`. `Denied` returns the existing
   `Organizations.JoinAdmissionRejected` conflict.
4. `Unavailable`, `Unknown`, an undefined value, or a non-cancellation policy
   exception fails closed with a new stable
   `Organizations.JoinAdmissionUnavailable` error and HTTP 503.
5. Caller-requested cancellation continues to propagate.
6. Admission is evaluated before source capacity, membership, or claim state is
   mutated. Availability failure therefore leaves all aggregates and events
   unchanged.
7. Exact retries of already committed acceptance, claims, or decisions retain
   their current bounded replay behavior and do not re-run product admission.

## Ownership

- Organizations owns the provider-neutral decision contract, policy
  composition, exception containment, error code, HTTP mapping, and tests.
- BunkFy maps workspace `Allowed` to `Allowed`, `Restricted` to `Denied`, and
  `Unavailable` or invalid operational state to `Unavailable`. Product-specific
  staff plans, verified email, and termination semantics remain in Workspaces.
- GMA Framework needs no change. This is module policy semantics, not generic
  CQRS or HTTP infrastructure.
- GMA Skeleton consumes the updated module contract but does not invent a
  default product policy.

## Delivery

- [x] Add the strict Contracts decision enum and serialization guard.
- [x] Replace boolean policy composition with structured, fail-closed
  authorization and bounded exception logging.
- [x] Return stable 409 versus 503 errors from all three join paths without
  mutation on failure.
- [x] Align BunkFy's workspace policy and prove restricted, unavailable, exact
  source/subject, claim, and verified-email behavior.
- [x] Synchronize source solutions and run one consolidated non-Docker gate per
  changed repository at the completed slice boundary.
- [x] Publish Organizations, Skeleton, BunkFy backend, and root functional pins.
- [x] Verify CI for the functional pins, then close the task.

## Verification

- Focused Organizations admission, invitation, enrollment, endpoint, and enum
  coverage passed with 115 tests; focused BunkFy workspace onboarding coverage
  passed with 10 tests.
- The Organizations non-Docker gate passed boundaries, a zero-warning build,
  PostgreSQL and SQL Server migration drift, all 265 fast tests, and the
  vulnerable-package scan.
- The BunkFy backend gate passed source graph and package checks, a zero-warning
  build, all migration drift checks, architecture coverage, and every
  non-Docker test suite.
- The Skeleton consumer gate passed source solution, security, release,
  generated-selection, build, migration, and fast-test checks. Its
  documentation guard exposed and then verified the repaired index for the
  earlier HTTP state-conflict task; all 269 architecture guards passed.
- The BunkFy root lightweight gate passed latest-submodule, workspace graph,
  operations, deployed-flow fixture, and production-admission checks.
- Exact-commit CI passed for every functional pin: Organizations validation and
  security, Skeleton validation, security, and CodeQL, BunkFy backend validation
  and security, and BunkFy root validation, security, and both CodeQL languages.

Functional publication pins:

- Organizations: `67c56e58cf6fae1dc60d06712176d53ec1554f4a`
- GMA Skeleton: `5671b5994390923b81857b742cac02c7867e0cf0`
- BunkFy backend: `12ffd9c0d88fe50264b3bf1b008151215696addd`
- BunkFy root: `f5811f6c49ddaba7e295bfe48a7ce953ec82f076`

## Not In This Slice

- changing invitation recipient verification or join-source authorization;
- changing onboarding forms, roles, staff profiles, or notifications;
- retrying a policy inside Organizations;
- caching product admission decisions;
- adding distributed transactions across Organizations and product modules; or
- changing persistence schemas, migrations, source capacity, or token formats.
