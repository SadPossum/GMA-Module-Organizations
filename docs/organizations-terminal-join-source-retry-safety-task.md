# Organizations Terminal Join-Source Retry Safety Task

Status: implementation complete; publication pending
Date: 2026-08-09

## Goal

Make invitation revocation and enrollment-link disable safe to retry after the
transaction commits but the response is lost. Preserve current authorization,
optimistic concurrency, event emission, natural expiry, replacement semantics,
and the existing HTTP and Contracts request shapes.

## Audit Finding

Revocation and disable currently compare only the caller's expected version
with the current aggregate version. A successful terminal transition advances
that version, so an unchanged transport retry reports a version conflict even
though the requested result is the immediately preceding committed state.

These operations differ from mutable profile, lifecycle, and membership
commands. Revoked invitations and disabled enrollment links cannot transition
again. Their persisted terminal state, immediately preceding version, and
normalized actor therefore provide bounded exact-replay proof without a caller
operation id or an operation journal.

## Exact Replay Contract

1. Current join-source management authorization is required before both first
   execution and replay. Retry safety never restores or bypasses authority.
2. A revocation replay is exact only when the invitation is persisted as
   `Revoked`, its current version is exactly one beyond the supplied expected
   version, and `LastChangedBy` matches the normalized actor.
3. A disable replay is exact only when the enrollment link is persisted as
   `Disabled` with the same version and actor correlation.
4. Exact replay returns the current DTO without advancing versions, allocating
   an event id, publishing another event, or changing capacity.
5. Expired, accepted, superseded, rotated, or otherwise terminal sources never
   satisfy replay for the wrong transition.
6. A stale request that is not the immediately preceding matching transition
   retains the existing version or source-unavailable failure semantics.
7. The proof uses existing authoritative fields. No schema, migration, public
   DTO, integration event, HTTP request, or Contracts request change is needed.

## Ownership

- Organizations owns terminal source state, optimistic versions, actor
  normalization, replay classification, authorization, and event idempotence.
- Products automatically inherit the behavior through
  `IOrganizationJoinSourceManager`; they keep the same expected version while
  retrying an unchanged deny action.
- GMA Framework needs no change. Generic idempotency middleware would add
  transport coupling while knowing less than the source aggregate already
  proves.

## Delivery

- [x] Add aggregate-level exact replay predicates for invitation revocation and
  enrollment-link disable.
- [x] Return exact replay before allocating event ids or invoking a second
  domain transition.
- [x] Prove version, actor, terminal-kind, authorization, event, and aggregate
  stability with focused unit tests.
- [x] Prove commit/reload replay against PostgreSQL and keep both provider
  migration models drift-free.
- [x] Verify the existing Contracts facade and BunkFy workspace source manager
  inherit the behavior without a product-specific workaround.
- [ ] Run one consolidated non-Docker gate and one focused provider gate at the
  completed slice boundary, then publish exact consumer pins.

## Implementation Evidence

Verified locally on 2026-08-09:

- Organizations boundary checks and solution synchronization pass;
- the solution builds with zero warnings and zero errors;
- SQL Server and PostgreSQL migration drift checks pass with no schema change;
- all 247 unit, contract, and architecture tests pass, including exact replay,
  actor/version/kind mismatch, current authorization, stable event counts, and
  zero additional id allocation;
- the focused PostgreSQL commit/reload scenario passes and proves stable
  versions plus exactly one terminal outbox fact for each source;
- the package audit reports no known vulnerabilities; and
- the existing Contracts facade and BunkFy Workspaces manager continue to send
  the same expected-version requests, so no HTTP, DTO, generated client,
  Framework, Extensions, or product-domain change is required.

## Not In This Slice

- retry safety for secret-bearing issuance or replacement, which already uses
  caller-owned source ids and predecessor lineage;
- changing invitation acceptance, enrollment claims, join-request decisions,
  or natural-expiry behavior;
- treating a transition performed by another actor as the caller's exact
  replay;
- reversible source lifecycle, source reactivation, or token recovery;
- generic Framework idempotency middleware or an unbounded operation journal;
  or
- product access-plan cleanup, notification delivery, or UI redesign.
