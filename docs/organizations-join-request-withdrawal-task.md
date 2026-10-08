# Organizations Join-Request Withdrawal Task

Status: Organizations implemented and verified; BunkFy product adoption in progress
Date: 2026-08-09
Publication correction: 2026-08-11

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

Canonical BunkFy Backend contains the baseline Workspaces withdrawal consumer in
commit `52b345e6e22ba894536f7861b016bd1c5b715992`. Later cross-repository audit found
that durable out-of-order correlation, retention reconciliation, migration and
catalog cutover, and release evidence still required hardening. That candidate
is tracked in [BunkFy Backend draft PR #11](https://github.com/SadPossum/BunkFy.Backend/pull/11).
An open draft is review evidence only; it does not establish canonical, released,
deployed, or end-to-end product adoption.

Canonical BunkFy Web contains the withdrawal interaction in commit
`9e773c66864eb4ae4fe26dde60c14f317fca4d9f`. Web draft
[PR #23](https://github.com/SadPossum/BunkFy.Web/pull/23) adds recovery, scale,
and actor-switch hardening; it is not evidence that the backend rollout is
complete. The client must offer withdrawal only while an authoritative pending
application supplies the claim id and version.

## Delivery

- [x] Add the Organizations domain state, transition, contract status, and
  payload-minimal event.
- [x] Add the authenticated command and HTTP endpoint with subject isolation,
  expiry precedence, exact replay, and one-time capacity release.
- [x] Extend domain, application, contract, retention, and provider tests.
- [x] Land the baseline BunkFy Workspaces withdrawal consumer and distinct
  terminal state in commit `52b345e6e22ba894536f7861b016bd1c5b715992`.
- [x] Land the BunkFy join-page withdrawal interaction in commit
  `9e773c66864eb4ae4fe26dde60c14f317fca4d9f`.

### Downstream adoption (tracked separately)

- [ ] Merge and verify durable Workspaces out-of-order correlation, staged-data
  redaction, retention reconciliation, persistence constraints, and rollout
  guardrails. Candidate: [BunkFy Backend draft PR #11](https://github.com/SadPossum/BunkFy.Backend/pull/11).
- [ ] Merge the BunkFy Web recovery and scale hardening. Candidate:
  [BunkFy Web draft PR #23](https://github.com/SadPossum/BunkFy.Web/pull/23).
- [ ] Complete backend full gates, the single-version Workspaces cutover,
  catalog and migration approval, load evidence, and deployed end-to-end smoke.
- [ ] Record named merged commits and downstream CI, provider, migration, and
  deployment evidence before changing product adoption to complete.

## Verification

- all 219 Organizations unit tests pass;
- 11 focused Organizations PostgreSQL proofs pass for state, capacity, event
  privacy, active-source retention, and terminal cleanup;
- the canonical web interaction commit passed hosted Validate run `31324199360`
  and Security Baseline run `31324199344`;
- the canonical baseline backend consumer was followed by successful Validate
  run `31324337679` at commit `14498ce77300e8f68c127c7e80ff199a693a0778`.

Backend draft PR #11 and Web draft PR #23 record their own current local and
hosted evidence. Those draft results are review evidence, not canonical merge,
release, or deployment proof. Deployed end-to-end verification remains pending.

The 2026-08-09 version incorrectly marked BunkFy consumer and end-to-end product
alignment complete. This correction separates the reusable Organizations slice,
the baseline downstream commits, the open hardening drafts, and deployment
evidence.

## Not In This Slice

- owner cancellation beyond the existing reject decision;
- reopening or reusing a withdrawn claim through the same enrollment link;
- account-wide discovery of pending claims without a known organization or
  product-owned onboarding record;
- changing source disablement, rotation, or natural-expiry semantics;
- adding product roles, Staff fields, notifications, or redirect URLs to GMA.
