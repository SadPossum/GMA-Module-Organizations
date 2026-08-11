# Organizations Membership Inspection Task

Status: complete
Date: 2026-08-11
Completed: 2026-08-11

## Goal

Expose one small Organizations contract for composed handlers that already
hold an organization id, membership id, and subject id and need to reconcile a
delivered membership event with the currently retained membership, organization,
and scope-lifecycle state.

## Contract

- `IOrganizationMembershipInspector.FindAsync` accepts one exact
  `(organizationId, membershipId, subjectId)` tuple.
- A retained exact tuple returns `OrganizationMembershipSnapshot` with only the
  organization and membership ids, organization status, scope status and
  revision, and membership role, status, and version.
- The subject is normalized and used as an exact, ordinal predicate but is not
  echoed in the result.
- A missing exact tuple returns `null`; suspended, removed, archived, and
  scope-closed records remain visible while retained.
- An existing organization with no legacy scope-state row maps to
  `Open` at revision `0`. A present row maps to `Open` or `Closed` with its
  current version.
- Empty ids and malformed subjects fail before persistence is queried.
  Provider failures and caller cancellation propagate.

The snapshot is a point-in-time reconciliation observation. It does not grant
access, reserve authority, lock a mutation, or guarantee that state remains
unchanged after the query. Consumers processing a versioned source event apply
their own monotonic policy; when comparing versions, the observed
`MembershipVersion` must be at least the source event version. The inspector
itself returns state and does not make that decision.

## Implementation

- Organizations Contracts owns the interface and snapshot.
- Organizations Persistence implements one provider-neutral, no-tracking
  projection joining the exact organization and membership and left joining
  the scope state.
- Standard persistence composition registers the implementation as scoped with
  `TryAdd`, so a host can supply an alternative implementation before module
  registration.
- No schema, migration, HTTP API, integration event, catalog, clock, retry, or
  cache is added.

## Privacy And Ownership Boundaries

The snapshot deliberately excludes the subject id, organization name and slug,
actors, timestamps, emails, tokens, mutation-operation data, and product
profile state. Organizations owns the retained lifecycle values. Consumers own
event-version comparison, idempotent downstream effects, retry behavior, and
product authorization.

## Acceptance Criteria

- neighboring organization, membership, subject, and subject-case keys cannot
  match accidentally;
- current organization, scope, role, membership status, and versions are
  returned faithfully, including retained terminal states;
- legacy missing scope state maps to the documented open revision-zero value;
- each observation is one no-tracking query and a later call sees committed
  state changes;
- invalid input fails before dependencies are used, while provider and
  cancellation failures propagate;
- standard composition is scoped and preserves a host override;
- unit, boundary, migration-drift, package-audit, and PostgreSQL gates pass;
- no public or persistent surface beyond the Contracts snapshot and reader is
  introduced.

## Verification Evidence

Completed on 2026-08-11:

- the generated solution is synchronized and Organizations boundary checks
  pass;
- the complete solution builds with zero warnings and zero errors;
- SQL Server and PostgreSQL report no pending model changes;
- all 332 Organizations unit and contract tests pass, including 9 focused
  membership-inspector and composition tests;
- the transitive package audit reports no known vulnerabilities; and
- all 15 Docker integration tests pass, including the focused PostgreSQL proof
  of exact ordinal matching, one command per observation, no tracking, legacy
  scope fallback, current-state rereads, and closed-scope retained state.
