# Organizations Profile And Lifecycle Retry Safety Task

Status: complete
Date: 2026-08-09

## Goal

Make organization profile updates and public or administrative lifecycle
changes safe to retry after the transaction commits but the response is lost.
Keep optimistic concurrency, current authorization, product admission, domain
events, and provider-neutral transaction boundaries intact.

## Audit Finding

Profile and lifecycle commands currently identify an attempt only by the
aggregate's expected version. A successful mutation advances that version, so
an exact transport retry becomes an ambiguous version conflict. The handlers
also run product mutation admission before the aggregate detects stale input.
That can repeat externally implemented admission work for a request that can no
longer commit.

## Ownership

- Organizations owns caller operation identity, bounded aggregate replay proof,
  normalized intent equivalence, optimistic versions, and event emission.
- Public and administrative callers provide one non-empty operation id for each
  logical mutation and preserve it while retrying unchanged input.
- Products own UI attempt lifetime and reconciliation after a retry can no
  longer be proven.
- GMA Framework needs no change. Generic HTTP idempotency middleware or response
  caching cannot prove Organizations intent and would couple optional modules
  to transport policy.

## Exact Replay Contract

1. Each profile or lifecycle request requires a caller-owned non-empty
   operation id.
2. The organization stores only the immediately preceding replayable operation
   id and mutation kind. Profile values, lifecycle state, actor, and resulting
   version remain the authoritative result proof.
3. An exact retry must match the operation id, mutation kind, normalized actor,
   normalized requested result, and a current version exactly one beyond the
   supplied expected version.
4. Exact replay returns the current organization without advancing a version,
   emitting another event, or rerunning product admission.
5. Reusing the current proof's operation id with changed action, actor, input,
   or expected version fails with a stable operation conflict.
6. A new operation id with a stale expected version fails before product
   admission.
7. Every later organization-root mutation clears an older replay proof. A
   caller whose retry is no longer the immediately preceding mutation must
   refetch and reconcile current state.
8. Domain event ids remain server-generated and distinct from caller operation
   ids.

## Persistence And Exposure

Add nullable `LastMutationOperationId` and `LastMutationKind` columns to the
organization row. Existing organizations remain valid with no replay proof.
No index or operation journal is needed because replay always loads the
organization by its primary key.

The proof is internal control metadata, like the creation fingerprint. It is
not returned by public or administration DTOs, events, logs, or the portable
scope export. BunkFy therefore does not add opaque operation identifiers to its
personal-data catalogue.

## Delivery

- [x] Add aggregate proof semantics, stable errors, and handler ordering.
- [x] Require operation ids in public API, administration API, and CLI
  lifecycle commands.
- [x] Add SQL Server and PostgreSQL migrations plus focused model coverage.
- [x] Prove exact replay, changed reuse, stale rejection before admission,
  event/version stability, and later-mutation invalidation.
- [x] Align the GMA Skeleton and BunkFy generated contracts and preserve the
  workspace-settings operation id across unchanged retries.
- [x] Run focused checks while editing, then one consolidated non-Docker gate
  and one focused PostgreSQL gate at the completed slice boundary.

## Verification

- canonical solution synchronization check passed;
- Organizations boundaries, build, PostgreSQL and SQL Server migration drift,
  and package vulnerability checks passed;
- Organizations fast suite passed: 238 tests;
- focused PostgreSQL commit-and-reload proof passed: 1 test; and
- BunkFy generated contracts, typecheck, and frontend suite passed: 255 tests.

## Not In This Slice

- retry safety for membership mutations or historical replay after another
  organization mutation;
- an unbounded operation journal, generic idempotency middleware, or stored HTTP
  responses;
- product workspace lifecycle UI or product-specific authorization;
- using caller operation ids as domain-event ids; or
- changing tenant export, personal-data classification, or restore policy.
