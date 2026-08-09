# Organizations Join-Subject Consistency Task

Status: completed
Date: 2026-08-09

## Goal

Give concurrent invitation and enrollment paths one deterministic winner per
organization and subject, without serializing unrelated applicants or moving
product onboarding policy into Organizations.

## Audit Finding

The membership table correctly enforces one row per organization and subject,
and the persistence pipeline retries one unique-constraint conflict. That
protects the final access fact, but it does not protect the admission workflow
that precedes it:

- enrollment claims are unique only by enrollment link and subject, so one
  subject can reserve capacity and create pending requests through several
  links in the same organization;
- concurrent membership-creating sources can both pass their precondition and
  product-policy reads before one transaction loses at the membership unique
  index and re-executes;
- a pending request can compete with invitation acceptance or another approved
  request, leaving an unnecessary source transition or reserved claim behind;
- accepting a still-pending invitation for an already-active member currently
  consumes the invitation and emits another accepted-source fact, unlike the
  existing enrollment behavior that rejects active members.

The database remains valid, but the operational result is ambiguous and can
cause duplicate product onboarding work. A generic Organizations module should
make the winning join attempt explicit before it publishes a terminal source
fact.

## Ownership

- Organizations owns membership uniqueness, join-source lifecycle, pending
  enrollment claims, capacity, and the order in which those states may admit a
  subject.
- GMA Framework continues to own the provider-neutral transaction key lock. Its
  existing PostgreSQL and SQL Server implementations are sufficient; no
  Framework change is required.
- Products own source preparation, applicant profile data, access plans, and
  additional admission policy. A product policy may deny the winning attempt,
  but it does not define Organizations concurrency.
- BunkFy continues to consume Organizations through Contracts and keeps Staff,
  workspace, property, and role vocabulary outside this slice.

## Consistency Contract

1. Coordination is scoped by normalized `(organizationId, subjectId)`.
   Different subjects in one organization and the same subject in different
   organizations do not block one another.
2. Membership-creating join paths acquire the organization governance lock
   first and an exclusive join-subject lock second. No handler reverses this
   order or upgrades a lock.
3. Token syntax, digest verification, and source lookup may happen before the
   organization id or subject lock is available. Mutable organization,
   membership, pending-request, and product-policy reads happen after the
   applicable locks.
4. An exact replay of an already accepted invitation or an existing enrollment
   claim returns its current source-correlated result and does not create a new
   winner.
5. A still-pending invitation cannot be accepted for an already-active member.
   It returns the existing `Organizations.MembershipConflict` and remains
   unconsumed. An inactive membership may still be restored by a valid source.
6. At most one not-overdue pending enrollment request may exist for a subject
   in an organization. An exact retry through the same link returns that claim;
   a different link returns a stable `Organizations.JoinRequestConflict`
   without reserving capacity.
7. A current pending enrollment request wins over a later invitation attempt.
   The invitation remains available until the request is resolved or expires.
8. An overdue pending claim does not block a new source. Exact retries retain
   the existing synchronous expiry behavior, and the lifecycle worker remains
   responsible for durable background expiry and capacity release.
9. Approval of the winning pending claim acquires the same join-subject lock
   before it creates or restores membership. Competing invitation, automatic
   enrollment, or approval paths then re-read the committed winner.
10. Rejection and background expiry do not create membership and need no
    join-subject lock. Their existing aggregate concurrency remains the source
    of truth.
11. The membership unique index and persistence retry remain defense in depth;
    normal same-subject competition should be resolved before a failed insert.
12. Subject values are never written to a database lock resource. The
    coordinator derives a fixed-length digest for its transaction key.

## Delivery

- [x] Add an application port and EF adapter for transaction-scoped
  organization/subject coordination.
- [x] Add an indexed repository predicate for a current pending enrollment
  request by organization and subject.
- [x] Apply the lock and single-winner checks to invitation acceptance,
  enrollment claiming, and enrollment approval.
- [x] Map the stable pending-request conflict to HTTP `409` without changing
  token secrecy or exposing product data.
- [x] Extend the boundary guard with join-subject classification and lock-order
  checks.
- [x] Add focused unit tests for exact replay, active membership, pending
  precedence, and acquisition order.
- [x] Add one PostgreSQL proof for same-subject competition and unrelated-
  subject concurrency, reusing a single container invocation for the slice.
- [x] Run the completed-slice Organizations gates, then verify GMA Skeleton and
  BunkFy consumers. Regenerate OpenAPI clients only if the public schema changes.

## Module Verification

- The boundary guard passes with explicit join-subject classification and
  governance-before-subject lock ordering for all three membership-creating
  handlers.
- The solution builds with zero warnings, all 206 unit tests pass, and both
  PostgreSQL and SQL Server migration models report no drift.
- The focused PostgreSQL scenario passes in one container invocation. It proves
  one winner and one reserved slot across competing links, same-subject
  blocking, and unrelated-subject concurrency.
- The transitive package vulnerability audit reports no vulnerable packages.
- `git diff --check` reports no whitespace errors. Existing repository line-
  ending normalization notices remain non-errors and were not broadened into
  unrelated formatting work.

## Consumer Verification

- GMA Skeleton at `4e467a5`, with Organizations at `1b174a1`, passes
  `eng/verify.ps1 -SkipRestore`: source synchronization, zero-warning builds,
  migration drift, architecture, integration, and all non-Docker suites are
  green.
- BunkFy backend at `a9657eb1`, with Organizations at `1b174a1`, passes
  `eng/verify.ps1 -SkipRestore`: source synchronization, a zero-warning build,
  all migration drift checks, 94 architecture tests, 54 integration tests, and
  every non-Docker module suite are green.
- The slice changes error behavior and persistence internals without changing
  public contract shapes. No OpenAPI or TypeScript regeneration was required,
  and the BunkFy web application was not changed.

## Not In This Slice

- product Staff/profile/access-plan deduplication or compensating workflows;
- auto-rejecting or adding a new terminal status to historical duplicate
  pending claims;
- changing invitation recipient verification or enrollment approval policy;
- replacing optimistic concurrency or the persistence retry pipeline;
- distributed locks across module databases or remote services;
- changing organization lifecycle, ownership, or membership-management rules.
