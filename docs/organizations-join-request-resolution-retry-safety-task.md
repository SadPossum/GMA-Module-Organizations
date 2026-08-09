# Organizations Join-Request Resolution Retry Safety Task

Status: implemented; consumer verification pending
Date: 2026-08-09

## Goal

Make approval-required enrollment decisions safe to retry after a committed
response is lost, without weakening authorization, optimistic concurrency, or
the single-winner join-subject contract.

## Audit Finding

Approval and rejection are terminal state-setting operations, but an exact
retry currently observes the advanced claim version and returns a version
conflict. That leaves an authorized operator unable to distinguish a failed
decision from a committed decision whose response was lost.

Blindly accepting every request whose target state already exists would be too
broad. Automatic enrollment also creates accepted claims, and arbitrary stale
versions must not bypass the normal join-request transition.

## Ownership

- Organizations owns enrollment-claim state, membership correlation, link
  capacity, authorization order, and terminal replay semantics.
- GMA Framework transaction and lock primitives are already sufficient; no
  Framework change is required.
- Products continue to own additional join admission policy. A replay reports
  a committed Organizations decision and must not execute product admission a
  second time.
- BunkFy remains a Contracts-only consumer. No product vocabulary or HTTP
  contract change belongs in this slice.

## Retry Contract

1. Governance coordination and current resolver authorization run before any
   terminal replay is returned.
2. A replay is exact only when the requested decision matches the persisted
   terminal state and `ExpectedClaimVersion` is the immediately preceding
   claim version.
3. An approval replay returns the authoritative accepted claim only when its
   organization is active and its correlated membership still exists, is
   active, and has the recorded membership id.
4. Approval replay does not rerun product admission, create or restore a
   membership, advance versions, or emit another domain event.
5. A rejection replay returns the authoritative rejected claim with no
   membership and never releases link capacity again.
6. Opposite decisions, automatic accepted claims, unrelated stale versions,
   expired claims, and broken membership correlation retain fail-closed
   conflict behavior.
7. The public request and response shapes do not change.

## Delivery

- [x] Recognize only exact accepted and rejected decision replays.
- [x] Reconstruct accepted outcomes from the current correlated membership.
- [x] Prove product admission is not rerun and resolver authorization is.
- [x] Prove rejection replay cannot release capacity reserved by later work.
- [x] Preserve conflict behavior for opposite decisions and broken
  correlations.
- [ ] Run focused unit tests, then one completed-slice Organizations gate and
  consumer verification before publication.

## Module Verification

- The focused enrollment flow passes all 24 tests.
- The completed-slice non-Docker gate passes boundary checks, a zero-warning
  solution build, PostgreSQL and SQL Server migration drift checks, all 211
  unit tests, and the transitive package vulnerability audit.
- No persistence query, mapping, migration, provider, or lock implementation
  changed. A container run would repeat existing persistence evidence without
  exercising this application-only replay path.
- `git diff --check` reports no whitespace errors. Existing line-ending
  normalization notices remain non-errors.

## Consumer Verification

Pending against the exact implementation commit.

## Not In This Slice

- adding operation ids or changing HTTP schemas;
- replaying automatic enrollment as an owner approval;
- restoring suspended or removed memberships during a replay;
- changing join-source issuance, expiry, retention, or product onboarding;
- weakening optimistic concurrency for pending claims.
