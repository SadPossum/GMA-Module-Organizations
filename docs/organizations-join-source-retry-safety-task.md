# Organizations Join Source Retry Safety Task

Status: implementation complete; consumer alignment pending
Date: 2026-08-09

## Goal

Make invitation and enrollment-link issuance safe across concurrent requests,
lost HTTP responses, and changed source-id reuse while preserving one-time
plaintext-token delivery and Organizations ownership boundaries.

## Audit Finding

The Contracts-only `IOrganizationJoinSourceIssuer` already requires a
caller-owned source id and treats it as the durable issuance identity. Exact
sequential retries return source metadata without replaying the plaintext token,
and changed reuse conflicts.

Two production gaps remain:

1. concurrent requests are not serialized before the existing-source check, so
   correctness currently depends on a unique-constraint failure and pipeline
   retry rather than a deterministic issuance outcome;
2. the generic HTTP creation endpoints still use server-generated source ids,
   so a retry after a lost response can create another live credential.

## Ownership

- Organizations owns source identity, normalized request equivalence, token
  digest storage, source constraints, owner authorization, and issuance outcome
  semantics.
- GMA Framework continues to own the provider-neutral transaction-scoped key
  lock. Organizations composes that primitive through a module-owned port; no
  Framework change is required.
- Products own staff/access plans, delivery, invitation URLs, browser retry
  lifetime, and recovery presentation when a token was already delivered.
- Plaintext invitation and enrollment secrets are never persisted, logged, or
  returned again after the creating call.

## Required Semantics

1. Every Contracts or HTTP issuance attempt requires one non-empty,
   caller-owned source id.
2. Validation and current authorization run before an id can reveal or replay
   source metadata.
3. Issuance acquires one provider-neutral exclusive transaction-key lock for
   the source id before checking existing source state or writing.
4. A source id is unique across invitation and enrollment-link kinds, not only
   within one table.
5. A new valid attempt runs organization and product admission, creates one
   source, stores only its token digest, and returns the plaintext token once.
6. An exact retry returns current source metadata with an `already-issued`
   outcome and no token. Admission is not rerun, but current authorization and
   active-organization checks still apply.
7. Reuse with different organization, kind, recipient, lifetime, claim limit,
   approval mode, subject, or actor fails with the stable join-source issuance
   conflict.
8. Validation, authorization, and failed admission do not bind a source id.
9. The HTTP response remains source-compatible for first issuance while adding
   an explicit replay outcome and nullable token for exact retries.
10. HTTP clients preserve the source id for retries of unchanged normalized
    intent and generate a new id only for a new issuance attempt.

## Delivery

- [x] Add a module-owned issuance coordinator backed by the existing EF
  transaction-key lock.
- [x] Enforce cross-kind source-id uniqueness in the authoritative issuance
  handlers.
- [x] Retire the duplicate server-id application creation paths and route HTTP
  creation through caller-id issuance.
- [x] Expose one-time-token and exact-replay outcomes without response caching.
- [x] Prove exact, conflicting, and cross-kind concurrent requests with focused
  PostgreSQL integration coverage.
- [ ] Align GMA Skeleton and BunkFy API consumers, then run consolidated module
  and consumer gates at the completed-slice boundary.

## Verification In Progress

- Organizations verification passed boundaries, build with zero warnings,
  SQL Server and PostgreSQL migration drift, 189 unit tests, dependency audit,
  and 9 Docker integration tests.
- GMA Extensions passed boundaries, build with zero warnings, 30 tests, and
  dependency audit against the changed Contracts surface.
- BunkFy backend passed source and solution guards, build with zero warnings,
  all migration drift checks, and the complete non-Docker test gate. Its focused
  authentication-assurance Docker test passed against the changed raw endpoint.
- BunkFy web passed typecheck, lint, 253 tests, and production build after
  regenerating OpenAPI contracts and aligning retry-aware issuance types.
- GMA Skeleton pinning and verification remain before the task is complete.

## Not In This Slice

- invitation reissue or enrollment-link rotation recovery after a lost token
  response;
- profile, lifecycle, membership, or ownership-transfer idempotency;
- generic idempotency middleware or plaintext response storage;
- product delivery, staff onboarding, or access-plan orchestration.
