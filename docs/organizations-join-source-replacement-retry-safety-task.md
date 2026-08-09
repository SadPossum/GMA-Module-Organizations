# Organizations Join Source Replacement Retry Safety Task

Status: in progress
Date: 2026-08-09

## Goal

Make invitation reissue and enrollment-link rotation recoverable after
concurrent requests or a lost HTTP response without persisting or replaying a
plaintext credential.

## Audit Finding

New invitation and enrollment-link issuance is retry-safe because the caller
owns the source id. Replacement operations still generate the replacement id
inside the handler. If the transaction commits but the response is lost, the
old source is terminal and the only plaintext replacement token is stranded.
A retry cannot identify the committed replacement and instead fails against the
changed predecessor.

Other optimistic-concurrency mutations can be reconciled by reading current
state. Secret-bearing replacement needs an explicit operation identity and
durable lineage because the token itself must remain one-time only.

## Ownership

- Organizations owns replacement identity, predecessor lineage, normalized
  request equivalence, serialization, token digests, atomic source transition,
  and retry outcomes.
- GMA Framework continues to own the provider-neutral transaction-scoped key
  lock. Organizations composes it for the predecessor and replacement ids; no
  Framework change is required.
- Products continue to own access plans, staff onboarding, delivery, URLs, and
  recovery presentation. The existing product workflow may keep revoking or
  disabling a source and issuing a caller-id source through Contracts.
- Plaintext secrets are returned only by the transaction that creates the
  replacement and are never persisted, logged, cached, or replayed.

## Required Semantics

1. Reissue and rotation require a non-empty caller-owned replacement source id
   distinct from the predecessor id.
2. Validation, current authorization, and active-organization checks run before
   replacement metadata can be returned.
3. The transaction acquires locks for predecessor and replacement ids in
   deterministic order before reading or mutating either source.
4. The replacement id remains globally unique across invitation and
   enrollment-link kinds.
5. A replacement stores its predecessor source id and predecessor version.
   Existing rows have no predecessor, and one predecessor can have at most one
   direct successor.
6. First execution atomically terminalizes the predecessor, creates one
   replacement with durable lineage, stores only the token digest, and returns
   the plaintext token with an `issued` outcome.
7. An exact retry matches organization, kind, predecessor id and version,
   normalized lifetime, subject, and actor. It returns current replacement
   metadata with `already-issued` and no token. Product admission is not rerun,
   but current authorization and organization state are still enforced.
8. Changed replacement-id reuse or changed intent fails with the stable
   join-source issuance conflict and never exposes another operation's source.
9. Concurrent replacements of one predecessor serialize. One operation may
   succeed; every competing operation receives a stable domain failure and no
   orphan replacement is created.
10. Validation, authorization, failed admission, or a failed predecessor
    transition does not bind the replacement id.
11. Enrollment-link disable remains a non-secret operation with its own command
    and response; it does not accept a replacement id or issuance outcome.
12. Scope export and source DTOs preserve replacement lineage. Persistence uses
    nullable lineage columns and unique indexes without retention-blocking
    self-referencing foreign keys.
13. Clients preserve a replacement id while retrying unchanged intent. If an
    exact retry reports `already-issued`, recovery uses the returned replacement
    as the predecessor of a deliberate new replacement with a new id; the old
    plaintext token is never reconstructed.

## Delivery

- [x] Add invitation and enrollment-link predecessor lineage to domain,
  persistence, mappings, scope export, and both provider migrations.
- [x] Extend the module issuance coordinator to lock predecessor and replacement
  ids in deterministic order.
- [x] Make invitation reissue caller-id-based and retry-aware.
- [x] Split enrollment-link disable from caller-id-based retry-aware rotation.
- [x] Align HTTP requests and responses with explicit replacement ids,
  nullable tokens, and issuance outcomes.
- [x] Prove exact retry, changed retry, same-predecessor competition, and
  cross-kind replacement collision with focused unit and PostgreSQL coverage.
- [ ] Align GMA Skeleton and BunkFy generated consumers, then run consolidated
  module and consumer gates at the completed-slice boundary.

## Migration Plan

- Add nullable predecessor id and version columns to invitation and
  enrollment-link tables.
- Add one unique nullable predecessor-id index per source kind so a predecessor
  has at most one direct successor.
- Do not add a self-referencing foreign key: terminal predecessors may be
  removed by retention while retained successors keep non-secret audit lineage.
- Existing sources remain valid with null lineage; no data backfill is needed.

## Not In This Slice

- replaying, encrypting, escrow-storing, or reconstructing plaintext tokens;
- product access-plan replacement or delivery orchestration;
- retroactively recovering responses lost before this contract is deployed;
- profile, lifecycle, membership, ownership-transfer, or join-decision
  idempotency;
- generic framework idempotency middleware or response caching.
