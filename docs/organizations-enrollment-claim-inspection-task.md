# Organizations Enrollment Claim Inspection Task

Status: complete
Date: 2026-07-28

## Goal

Expose a small, authoritative Organizations contract that lets another composed
module reconcile one known enrollment claim by organization, enrollment link,
and subject. This closes delayed-event recovery gaps without exposing
Organizations persistence or moving product retention policy into GMA.

## Contract

- `IOrganizationEnrollmentClaimInspector` accepts one exact
  `(organizationId, enrollmentLinkId, subjectId)` key.
- A retained matching claim returns the existing
  `OrganizationEnrollmentClaimDto`, including its authoritative state,
  membership id, version, timestamps, and decision deadline.
- A missing row returns `null`.
- Invalid identifiers fail before persistence is queried.
- The contract does not list claims, search across subjects, expose join
  tokens, or authorize a public endpoint.

`null` means that no matching retained claim exists at observation time. It is
not permanent evidence that a claim never existed because Organizations may
remove old terminal claims under its configured retention policy. Consumers
using the inspector for recovery must reconcile within that retained-history
window.

## Implementation

- Implement the contract in Organizations persistence as a single exact,
  no-tracking projection.
- Keep the query provider-neutral and return only the existing public DTO.
- Register the implementation with the normal Organizations persistence
  composition.
- Cover exact-key isolation, all returned lifecycle data, no-tracking
  behavior, invalid input, dependency-injection registration, and one focused
  PostgreSQL translation/query-count proof.

## Ownership Boundaries

Organizations owns claim state, identifiers, membership correlation,
timestamps, and claim retention.

Consumers own their retry cadence, staging lifetime, copied profile data,
redaction, and recovery actions. No BunkFy-specific policy belongs in this
contract, GMA Extensions, or Framework.

## Acceptance Criteria

- one exact lookup returns the authoritative retained claim;
- neighboring organizations, links, and subjects cannot match accidentally;
- reads do not track or mutate the Organizations aggregate;
- invalid keys fail deterministically;
- the contract is available through standard module composition;
- the existing Organizations non-Docker gate remains green;
- one focused PostgreSQL proof confirms one-query, no-tracking behavior;
- no schema, migration, HTTP API, integration event, Framework, or Extensions
  change is required.

## Module Evidence

Completed on 2026-07-28:

- module boundary checks pass;
- the full solution builds with zero warnings and zero errors;
- SQL Server and PostgreSQL migration drift checks pass;
- all 93 Organizations unit and contract tests pass;
- the transitive package audit reports no known vulnerabilities;
- the focused PostgreSQL inspector proof passes with exactly one reader command
  and no tracked entities.

## Composition Evidence

Composition is complete as of 2026-08-09:

- the canonical Skeleton pins the published Organizations module and retains
  its normal persistence registration for the inspector;
- BunkFy Workspaces consumes `IOrganizationEnrollmentClaimInspector` in
  `ReconcileWorkspaceStaffOnboardingRetentionCandidateCommandHandler` instead
  of reading Organizations persistence;
- focused Workspaces retention tests cover pending, accepted, rejected,
  expired, withdrawn, missing, inconsistent, and authority-lapsed claim
  observations; and
- the BunkFy non-Docker verification gate covers the composed application and
  worker graph.

The product keeps its staging lifetime, cleanup decisions, and recovery state;
Organizations remains the sole authority for the retained claim projection.
