# Organizations Admission Contract Boundary Task

Status: in progress
Date: 2026-08-09

## Goal

Move organization-creation admission and recipient-bound invitation
verification from public Application ports to strict Contracts seams, while
distinguishing deliberate denial from temporarily unavailable provider state.

## Audit Finding

`IOrganizationAdmissionPolicy` and
`IOrganizationInvitationAdmissionPolicy` currently live in Organizations
Application and return arbitrary `Result` values. As a result, GMA's
Auth-Organizations extension and BunkFy's Workspaces extension reference the
module implementation layer, policy errors are not bounded by a stable
contract, and an Auth contact-reader failure escapes as an unclassified HTTP
500.

The two seams also have different composition semantics. Every product
creation policy must allow a fresh organization, while recipient verification
needs proof from any one trusted verifier. Encoding both as replaceable
Application services hides that distinction and makes additional adapters hard
to compose safely.

## Decision Contract

1. Organizations Contracts owns creation and recipient-verification requests,
   decisions, strict wire names, and policy interfaces. The legacy public
   Application ports are removed after all in-repository consumers migrate.
2. The module's `SelfServiceCreationEnabled` option remains the first creation
   gate. Disabled self service retains
   `Organizations.SelfServiceCreationDisabled` and HTTP 403.
3. With self service enabled, no installed product creation policies preserves
   standalone behavior. Every installed policy must return `Allowed`.
   Deliberate denial and subject-verification requirements retain stable HTTP
   403 results.
4. Creation `Unavailable`, `Unknown`, an undefined value, or a
   non-cancellation policy exception fails closed with a new stable HTTP 503
   result. Evaluation stops after the first failure.
5. Unbound invitations do not invoke recipient verifiers. A recipient-bound
   invitation requires `Verified` from at least one registered trusted
   verifier; no verifier or unanimous `NotVerified` returns the existing
   `Organizations.RecipientVerificationRequired` HTTP 403.
6. A recipient verifier's `Unavailable`, `Unknown`, undefined value, or
   non-cancellation exception is remembered while remaining verifiers may
   still prove the recipient. If none verifies, any indeterminate result wins
   over `NotVerified` and returns a new stable HTTP 503 result.
7. Caller-requested cancellation propagates. Policy logs identify only the
   provider type and decision category; they do not include subject ids,
   recipient addresses, organization names, or tokens.
8. Validation, locking, exact-replay checks, and optimistic behavior retain
   their current order. Fresh admission occurs before slug checks or aggregate
   mutation for creation, and before membership or invitation mutation for
   acceptance. Exact committed retries remain policy-free.

## Ownership

- Organizations Contracts owns provider-neutral decision vocabulary and
  requests; Organizations Application owns composition, option gating,
  exception containment, stable errors, and mutation placement; Organizations
  API owns HTTP mapping.
- GMA Extensions maps Auth's verified-contact capability to recipient proof and
  references Organizations Contracts only. Auth remains the owner of contacts
  and verification state.
- BunkFy Workspaces maps product workspace-creation configuration and verified
  contact state to the generic creation decision. Workspace modes, Auth scope,
  and onboarding behavior remain product-owned.
- GMA Framework needs no change. These are Organizations extension contracts,
  not generic CQRS, result, or policy-pipeline infrastructure.

## Delivery

- [x] Add strict creation and recipient-verification Contracts decisions,
  requests, interfaces, and serialization coverage.
- [x] Add scoped Organizations composers with denial, availability,
  exception, cancellation, ordering, and composition tests.
- [x] Route creation and invitation acceptance through the composers with
  exact-replay and no-mutation adversarial coverage.
- [x] Move GMA Auth-Organizations to a Contracts-only recipient verifier and
  tighten its boundary guard.
- [x] Move BunkFy Workspaces to a Contracts-only creation policy and add a
  product architecture guard.
- [ ] Run one consolidated non-Docker gate per changed repository at the
  completed slice boundary, publish exact pins, and verify CI.

## Verification Plan

- Use focused contract, composer, handler, API, extension, and architecture
  tests while editing.
- Run no Docker/provider gate because this slice changes no persistence model,
  query, migration, transaction boundary, or database-provider behavior.
- At the completed boundary, run the full non-Docker Organizations,
  GMA-Extensions, Skeleton, and BunkFy backend gates once, plus the BunkFy root
  lightweight gate before publication.

## Focused Verification Evidence

- Organizations boundary guard passed.
- Organizations focused contract, composer, handler, and API tests passed: 114.
- The explicit unbound-invitation verifier-bypass test passed: 1.
- GMA Auth-Organizations boundary guard and focused tests passed: 4.
- BunkFy Workspaces focused tests passed: 83; its Contracts-only architecture
  test passed: 1.

## Not In This Slice

- changing Auth contact storage, adding secondary-email lookup, or changing
  email-verification workflows;
- changing workspace creation, staff bootstrap, invitation, enrollment, or
  join-admission product rules;
- moving Organizations access-decision, candidate-filter, or scope-lifecycle
  Application ports to Contracts;
- retrying or caching provider decisions inside Organizations;
- changing persistence schemas, token formats, source capacity, or
  transactions; or
- introducing a generic Framework policy pipeline.
