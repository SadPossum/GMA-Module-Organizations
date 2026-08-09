# Organizations Join-Request Withdrawal Task

Status: implemented and verified
Date: 2026-08-09

## Goal

Allow an authenticated applicant to withdraw their own pending,
approval-required enrollment request without waiting for an owner decision or
natural expiry. The operation must be private, retry-safe, capacity-correct,
and reusable by composed products without moving product onboarding behavior
into Organizations.

## Audit Finding

Organizations owns durable pending claims, their decision deadlines, and the
reserved enrollment-link capacity behind them. Owners can approve or reject a
request and lifecycle maintenance can expire it, but the applicant cannot end
their own request.

This leaves an applicant's subject id and the product's staging record active
until an owner or worker acts, and keeps one link-capacity slot reserved even
after the applicant no longer wants access.

## Ownership

- Organizations owns the generic `Withdrawn` claim state, self-authorization,
  optimistic concurrency, capacity release, retention eligibility, and the
  durable integration fact.
- GMA Framework already provides the transaction, outbox, authentication, and
  coordination primitives required by the operation. No Framework change is
  required.
- Products own applicant profile staging, notifications, copy, navigation, and
  any product-specific recovery after withdrawal.
- A product policy cannot prevent a subject from retracting their own pending
  request. Product admission continues to govern claim creation and approval.

## Withdrawal Contract

1. The authenticated subject is normalized before coordination and is the only
   authority for the operation.
2. The command acquires the organization governance shared fence and the
   organization-subject join fence before loading mutable claim state.
3. A claim owned by another subject is indistinguishable from a missing claim.
   No claim, source, or applicant detail is disclosed.
4. Only an unexpired `Pending` claim can become `Withdrawn`.
5. If the decision deadline has already passed, the command persists the
   existing `Expired` transition instead of rewriting history as a withdrawal.
6. An exact retry with the immediately preceding version returns the current
   `Withdrawn` or `Expired` outcome without advancing versions, releasing
   capacity again, or publishing another event.
7. Withdrawal releases one reserved claim while the parent enrollment link is
   active and mutable. A terminal link keeps its historical counter unchanged,
   matching rejection and expiry behavior.
8. The authenticated HTTP surface uses an organization id, claim id, and
   expected version, requires authorization, and returns `no-store` responses.
9. A withdrawn claim remains while its enrollment link is active so the same
   subject cannot reuse that source. Existing bounded retention removes the
   terminal claim, then its parent link, only after the source is terminal and
   both records are older than the configured history window.

## Event Compatibility

- Existing `enrollment-claim-changed` v1 remains unchanged and continues to
  represent requested, accepted, and rejected transitions only.
- Withdrawal publishes a dedicated
  `OrganizationEnrollmentClaimWithdrawnIntegrationEvent` v1 through the
  Organizations outbox.
- The event contains only scope, organization, enrollment-link, and claim ids,
  the resulting claim version, and occurrence time. It contains no subject id,
  actor id, token, or product profile data.
- Consumers correlate by claim id and own their resulting product transition.

## Product Alignment

BunkFy Workspaces will consume the withdrawal event, transition the matching
Staff onboarding record to its own terminal `Withdrawn` state, redact staged
applicant profile data, and expose the result to the join flow. The web client
will offer withdrawal only while an authoritative pending application supplies
the claim id and version.

## Delivery

- [x] Add the Organizations domain state, transition, contract status, and
  payload-minimal event.
- [x] Add the authenticated command and HTTP endpoint with subject isolation,
  expiry precedence, exact replay, and one-time capacity release.
- [x] Extend domain, application, contract, retention, and provider tests.
- [x] Consume the event in BunkFy Workspaces with a distinct terminal state and
  database constraints that require staged profile redaction.
- [x] Add the BunkFy join-page withdrawal interaction and generated contract
  alignment.
- [x] Run focused tests while editing, then one completed-slice non-Docker gate
  and one focused provider gate before publication.

## Verification

- all 219 Organizations unit tests pass;
- all 343 BunkFy Workspaces unit tests pass;
- the complete BunkFy non-Docker verifier passes with a warning-free build,
  synchronized solution graph, clean migration models, and architecture guards;
- all 253 web tests, lint, typecheck, production build, OpenAPI snapshot, and
  generated-contract checks pass;
- focused PostgreSQL proofs pass for Organizations state, capacity, event
  privacy, active-source retention, terminal cleanup, and BunkFy projection,
  redaction, migration, and retention behavior.

## Not In This Slice

- owner cancellation beyond the existing reject decision;
- reopening or reusing a withdrawn claim through the same enrollment link;
- account-wide discovery of pending claims without a known organization or
  product-owned onboarding record;
- changing source disablement, rotation, or natural-expiry semantics;
- adding product roles, Staff fields, notifications, or redirect URLs to GMA.
