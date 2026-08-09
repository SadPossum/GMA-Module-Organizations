# Organizations Membership Mutation Retry Safety Task

Status: module implementation verified; composition publication pending
Date: 2026-08-09

## Goal

Make owner-initiated membership suspension, resumption, and removal safe to
retry after a committed response is lost. Preserve current authorization,
optimistic concurrency, product policy, governance coordination, and event
semantics.

## Audit Finding

The public membership commands carry organization and membership versions but
no caller-owned operation id. Retrying a committed request therefore reports a
version or state conflict. Suspending or removing the caller's own owner
membership is worse: the committed change removes the authority required to
observe the result on retry.

The trusted `IOrganizationMembershipLifecycle` facade is already
desired-state idempotent and protects owner memberships. It is not part of this
gap.

## Ownership

- Organizations owns retry identity, membership state, owner-count
  correlation, authorization, versions, product-policy ordering, and event
  idempotence.
- GMA Framework needs no change. Generic middleware cannot prove the bounded
  aggregate state or safely restore authority to a caller whose own membership
  was changed.
- Products own operation-id lifetime, confirmation and recovery UX, and
  reconciliation of product-owned staff and access state.

## Retry Contract

1. Public suspend, resume, and remove requests require a non-empty caller-owned
   operation id. A caller keeps it only while retrying the same target and
   action.
2. The target membership retains bounded proof for only its immediately
   preceding owner-initiated lifecycle mutation. Any later membership mutation
   clears or replaces that proof.
3. An exact retry must match the operation id, action, target aggregate,
   immediately preceding membership version, resulting state, and actor.
4. A target owner mutation must also match the immediately preceding
   organization owner-count change, including operation id, organization
   version, actor, and transaction timestamp.
5. Exact replay returns the current membership without advancing versions,
   changing owner counts, rerunning product policies, or publishing events.
6. Reusing the retained operation id with changed intent returns the stable
   mutation-operation conflict. A new operation with a stale version follows
   normal optimistic-concurrency behavior.
7. Current owner authorization is still required before ordinary replay. The
   only exception is an exact, correlated replay of the caller suspending or
   removing their own owner membership; no other failed authorization may use
   the replay path.
8. Operation proof is internal control metadata. It is not exposed in public
   DTOs, integration events, scope export, or logs.

## Delivery

- [x] Add bounded retry proof to the membership aggregate and correlate owner
  count changes on the organization root.
- [x] Require and map operation ids through application and public HTTP
  contracts.
- [x] Prove exact member and owner replays, self-authority loss recovery,
  changed-intent conflict, stale-version rejection, later-mutation
  invalidation, policy non-reexecution, and event/version stability.
- [x] Persist the proof in SQL Server and PostgreSQL and prove commit/reload
  behavior with one focused provider test.
- [ ] Align the canonical Skeleton, BunkFy backend contract, and BunkFy caller
  contract without leaking workspace semantics into GMA. BunkFy has no direct
  caller for these routes today; any future caller must retain one operation id
  for one unchanged target/action attempt.
- [ ] Run one consolidated non-Docker gate and one focused provider gate at the
  completed slice boundary, then publish exact consumer pins.

## Not In This Slice

- changing the desired-state `IOrganizationMembershipLifecycle` facade;
- adding operation ids to ownership transfer or join-source contracts that
  already have separate retry semantics;
- replay after a later target-membership or correlated owner-count mutation;
- generic idempotency middleware, an unbounded operation journal, or stored
  HTTP responses;
- product staff lifecycle, role assignment, access cleanup, or notification
  behavior; or
- changing whether inactive organizations permit owner-governed membership
  cleanup, which is a separate lifecycle-policy decision.

## Module Evidence

Verified on 2026-08-09:

- source-boundary checks and the zero-warning solution build passed;
- SQL Server and PostgreSQL migration models are drift-free;
- all 244 non-Docker Organizations tests passed;
- the transitive package vulnerability audit found no vulnerable packages; and
- the focused PostgreSQL commit/reload and self-authority-loss scenario passed
  with one mutation fact and stable aggregate versions on replay.
