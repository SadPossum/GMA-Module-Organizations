# Organizations Creation Idempotency Task

Status: complete
Date: 2026-08-09

## Goal

Make self-service organization creation safe across lost responses,
concurrent retries, process restarts, and changed operation reuse without
moving product workspace onboarding into Organizations.

## Audit Finding

`POST /api/organizations` currently generates all identities inside the
handler. If the transaction commits but its response is lost, the caller has
no organization id to continue with. Retrying creates a second logical attempt
and normally fails on slug or product admission; a different slug could create
a duplicate organization where the host permits multiple memberships.

Invitation and enrollment-source issuance already establish the reusable GMA
precedent: the caller owns a stable source identity, exact retries return the
existing resource, and changed reuse fails. Organization creation needs the
same durable operation semantics, but it does not need token outcome contracts
or response caching.

## Ownership

- Organizations owns creation operation identity, normalized request
  equivalence, organization and owner-membership atomicity, and provider-neutral
  concurrent serialization.
- The public Organizations API requires one non-empty operation id for each
  logical creation attempt.
- Products own workspace terminology, browser retry-attempt lifetime,
  post-creation staff profiles, access provisioning, and restrictions such as
  allowing a subject to join only one workspace.
- GMA Framework continues to own the existing transaction-scoped key-lock
  primitive. No Framework change is required.

## Required Semantics

1. Creation requires a non-empty caller-owned operation id and uses it as the
   immutable organization id.
2. Name, slug, subject id, and actor id are validated and normalized before an
   operation can bind.
3. The command acquires a provider-neutral exclusive transaction-key lock for
   the creation operation before checking existing state or writing. Scope
   destruction reuses the same stable per-organization lock resource before
   any lifecycle read or close, preserving coordination with older creation
   nodes during a rolling upgrade.
4. A new valid attempt evaluates self-service/product admission, verifies slug
   availability, and commits the organization, initial owner membership,
   immutable creation fingerprint, scope state, events, and outbox atomically.
5. An exact retry matches normalized name, slug, subject, and actor against the
   immutable fingerprint. It returns the current organization and the original
   subject's current active membership without another mutation or admission
   check. A former member cannot use an old operation id as an access path.
6. Changed reuse, a legacy organization without creation metadata, or missing
   result membership fails with a stable creation-operation conflict.
7. Validation and failed admission do not bind a new operation id.
8. Slug conflicts belonging to another operation retain the existing stable
   slug-conflict response.
9. Scope destruction remains an anti-resurrection barrier because the
   operation id and organization scope id are the same. Concurrent replay and
   destruction serialize on that identity. A replay that follows closure must
   re-read the tombstone; destruction cannot overlap a transaction that is
   still creating or replaying the same identity.
10. Browser callers preserve operation identity while normalized name and slug
    are unchanged, rotate it when intent changes, and clear it after a known
    successful response.

## Persistence

- Add a nullable, fixed-length lowercase SHA-256 creation fingerprint to the
  organization row. Existing organizations remain valid but cannot be claimed
  as replays of new caller-owned operations.
- Keep the fingerprint internal: it is not returned by API, administration,
  events, logs, exports, or public contracts.
- Add additive SQL Server and PostgreSQL migrations. No operation table,
  plaintext request snapshot, or generic response cache is needed because an
  organization has exactly one creation operation and owns its lifecycle.

## Delivery

- [x] Add operation identity, stable errors, normalized fingerprinting, and
  exact replay behavior to Organizations Application and API.
- [x] Add provider-neutral creation locking and persistence configuration.
- [x] Add SQL Server and PostgreSQL migrations with drift coverage.
- [x] Prove validation, admission ordering, exact replay, changed reuse,
  legacy-state conflict, and concurrent identical/different attempts.
- [x] Align GMA Skeleton and BunkFy contracts and BunkFy's workspace onboarding
  retry attempt without adding product concepts to Organizations.
- [x] Run focused checks while editing, then consolidated repository and
  consumer gates at the completed-slice boundary.

## Not In This Slice

- profile or lifecycle update idempotency;
- membership, ownership-transfer, invitation, or enrollment management changes;
- staff profile creation or access-grant orchestration;
- generic framework idempotency middleware or response storage;
- multi-step product workflow compensation.
