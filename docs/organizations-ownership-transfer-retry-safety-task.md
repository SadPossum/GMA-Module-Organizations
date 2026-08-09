# Organizations Ownership Transfer Retry Safety Task

Status: implemented and verified
Date: 2026-08-09

## Goal

Make an owner-initiated ownership transfer safe to retry after the server
commits but the caller loses the response. Preserve the existing HTTP and
Contracts shapes, optimistic versions, authorization, product admission, and
multi-aggregate transaction boundary.

## Audit Finding

Ownership transfer promotes the target membership, demotes the initiating
owner, and advances organization governance in one transaction. A successful
transfer therefore removes the caller's owner authority before its response is
delivered. If that response is lost, the same request currently fails owner
authorization even though the requested transfer committed.

This is operationally ambiguous for clients and encourages unsafe retries with
fresh versions. The BunkFy workspace screen also starts this destructive change
from a single button without an explicit confirmation or post-error
reconciliation read.

## Ownership

- Organizations owns exact transfer replay, authority checks, aggregate
  versions, owner-count invariants, and event idempotence.
- GMA Framework needs no change. A generic request cache cannot prove the
  domain-specific relationship among the organization and two memberships or
  decide whether a demoted caller may still observe a prior result.
- Products own confirmation copy, presentation, refresh behavior, and recovery
  when a transport result is ambiguous.

## Exact Replay Contract

1. A transfer continues to acquire exclusive organization governance before
   reading mutable authority or transfer state.
2. The normal path still requires the caller's active owner membership and runs
   product mutation admission before changing state.
3. The existing organization, current-owner, and target expected versions form
   the retry identity. No weaker stale request becomes a replay.
4. A demoted caller may receive the committed target membership only when all
   current records prove the immediately preceding atomic transfer:
   - the caller remains the exact active membership and is now a member;
   - the target is the exact active owner requested by the command;
   - organization and former-owner versions are each exactly one beyond the
     supplied versions;
   - the target version is either unchanged because it was already an owner or
     exactly one beyond the supplied version because it was promoted;
   - server actor and change timestamps agree across every record changed by
     the transfer.
5. An exact replay returns the current target membership without advancing any
   version, changing owner counts, publishing events, or rerunning product
   admission.
6. Any mismatch follows normal authorization and concurrency behavior. The
   replay path never grants mutation authority back to the former owner.

## Product Alignment

BunkFy will replace the one-click `Make owner` action with an explicit modal
that names both the authority gain and the current owner's demotion. A failed
transport attempt will refetch workspace membership state before presenting a
retry so a committed transfer is reflected without guesswork.

## Delivery

- [x] Add state-proven exact replay to the Organizations ownership-transfer
  handler without changing public request or response contracts.
- [x] Prove promoted-target and already-owner replay, event/version stability,
  policy non-reexecution, and false-replay rejection.
- [x] Add BunkFy confirmation and post-error reconciliation for ownership
  transfer.
- [x] Run focused checks while editing and one consolidated non-Docker gate at
  the completed slice boundary. No provider gate is required because the slice
  changes no persistence model or provider query.

## Verification

- Organizations focused governance and mutation-admission tests: 25 passed.
- Organizations boundary checks and `.slnx` loading: passed.
- BunkFy backend `eng/verify.ps1 -SkipRestore`: zero-warning build, migration
  drift checks, and all non-Docker suites passed, including 223 Organizations
  tests.
- BunkFy web `pnpm verify`: typecheck, lint, 253 tests, and production build
  passed.
- Generated web contract drift check: passed.

## Not In This Slice

- profile, lifecycle, or ordinary membership mutation retry semantics;
- replay after any of the three transfer records changes again;
- generic HTTP idempotency middleware or response storage;
- restoring former-owner authority or undoing a transfer;
- product roles, Staff employment state, or access-profile assignment.
