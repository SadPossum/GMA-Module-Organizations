# Organizations Governance Transaction Consistency Task

Status: completed
Date: 2026-08-09

## Goal

Make organization lifecycle and membership authorization stable for the full
transaction that relies on it, without serializing unrelated organizations or
ordinary work within one active organization.

## Audit Finding

Organizations commands run in transactions with the provider default
isolation level. Under `READ COMMITTED`, reading an active owner, member, or
organization does not prevent a concurrent transaction from changing that
governance state.

Several authorized commands update only a join source or another membership.
They do not update the authorizing membership or organization row, so optimistic
concurrency cannot detect a transfer, suspension, removal, or organization
lifecycle change that commits after authorization but before their commit.
The result is a time-of-check/time-of-use gap: work may commit after the subject
lost authority or the organization stopped accepting that work.

## Ownership

- Organizations owns the meaning and serialization of its lifecycle,
  membership, ownership, invitation, enrollment, and join-decision state.
- GMA Framework continues to own the provider-neutral transaction-scoped key
  lock. Its existing shared and exclusive modes support PostgreSQL and SQL
  Server, so no Framework change is required.
- Products own business permissions and admission policies layered on top of
  Organizations. A product policy that depends on product-owned mutable state
  remains responsible for making that state transactionally safe.
- BunkFy continues to use Organizations through Contracts. No hostel, staff,
  workspace, or property policy belongs in this slice.

## Consistency Contract

1. Coordination is scoped by organization id. Unrelated organizations never
   block one another.
2. A transaction acquires at most one governance mode for an organization and
   acquires it before reading organization lifecycle or membership state used
   for mutation authorization.
3. Ordinary authorized mutations acquire a shared governance lock. Shared
   operations may proceed concurrently and retain existing aggregate-level
   optimistic concurrency.
4. Lifecycle, ownership, role, and membership-state mutations acquire an
   exclusive governance lock before reading or changing governance state.
5. A shared operation that acquired the lock first may commit before a waiting
   governance change. A governance change that acquired the lock first commits
   before a waiting operation re-reads and evaluates the new state.
6. Commands never upgrade a shared lock to exclusive. This avoids lock-upgrade
   deadlocks on every supported provider.
7. Input validation that does not read mutable governance state may run before
   lock acquisition. All relevant repository reads and extension policy calls
   run after acquisition.
8. Existing source-specific locks retain their purpose and are acquired after
   the organization governance lock. The global order is governance first,
   then join-source keys in their existing deterministic order.
9. Token-based invitation acceptance and enrollment claims may resolve and
   verify the opaque token before the organization id is known. They acquire
   the shared governance lock immediately after resolution and before reading
   organization or membership state. Source-row optimistic concurrency remains
   responsible for concurrent revoke, disable, accept, or claim transitions.
10. Query handlers do not acquire transaction locks. Their authorization is a
    point-in-time read and they do not commit protected state.
11. Background source-expiry work remains outside the governance fence. It is
    authorization-independent and already uses source aggregate concurrency.
12. Coordination requires the command transaction established by the CQRS unit
    of work. Missing transaction or unsupported-provider failures are explicit;
    production never silently falls back to process-local locking.

## Operation Matrix

Shared governance lock:

- update organization profile;
- issue, reissue, or revoke an invitation;
- issue, rotate, or disable an enrollment link;
- resolve an enrollment join request;
- accept an invitation;
- claim an enrollment link.

Exclusive governance lock:

- suspend, reactivate, or archive an organization;
- suspend, resume, or remove a membership;
- transfer ownership;
- trusted administration lifecycle changes;
- trusted administration owner recovery;
- product-driven membership lifecycle reconciliation.

No governance lock:

- create a new organization;
- invitation, enrollment-link, and enrollment-claim expiry maintenance;
- read-only queries and token previews.

## Delivery

- [x] Add an application port with explicit shared and exclusive governance
  acquisition and an EF implementation keyed by organization id.
- [x] Apply the matrix to every transactional handler before governance reads.
- [x] Register the EF coordinator without exposing persistence through public
  Contracts or requiring a Framework change.
- [x] Add focused unit coverage for mode and acquisition ordering.
- [x] Add PostgreSQL concurrency coverage proving both serialization orders,
  parallel shared work, and independence across organizations.
- [x] Add a guardrail that fails when a newly added organization-scoped command
  bypasses an explicit governance classification.
- [x] Run the completed-slice Organizations gates, then verify GMA Skeleton and
  BunkFy consumers without regenerating contracts unless the public surface
  actually changes.

## Module Verification

- The strengthened boundary gate passes and classifies every command handler as
  shared, exclusive, or intentionally uncoordinated.
- The solution builds with zero warnings and both PostgreSQL and SQL Server
  migration models report no drift.
- All 197 unit tests pass, including acquisition-mode and ordering coverage.
- The focused PostgreSQL container scenario passes both lock orderings,
  concurrent shared work, and independent-organization work in one container
  run.
- The transitive package vulnerability audit reports no vulnerable packages.
- Repo-wide `dotnet format --verify-no-changes` is not a usable module gate yet:
  it reports pre-existing line-ending, encoding, whitespace, and analyzer
  findings across untouched Organizations and Framework sources. No unrelated
  formatting was changed in this slice.

## Consumer Verification

- GMA Skeleton passed its full non-Docker verification at consumer commit
  `c0dc8a8a42410a28cb6b7dcce945515b0d85f86f`, including solution and source
  package synchronization, a zero-warning build, both-provider migration drift,
  and all fast test suites.
- BunkFy backend passed its full non-Docker verification with Organizations
  implementation commit `7aa2f54a3416b793dd4766403815fa8078ddf592`, including
  a zero-warning build, all migration drift checks, 197 Organizations tests,
  94 architecture tests, and 54 host integration tests.
- The slice does not change Organizations Contracts, HTTP endpoints, OpenAPI,
  or generated TypeScript. The BunkFy web consumer therefore required no
  regeneration or source change.
- This completion update changes documentation only; consumers pin its final
  source commit through their normal source-package synchronization checks.

## Not In This Slice

- cross-module distributed transactions or locking product-owned data;
- serializable isolation for every Organizations command;
- command response caching or generic idempotency middleware;
- changing product permissions, workspace roles, or BunkFy onboarding rules;
- solving same-subject joins through different sources beyond existing unique
  constraints and persistence retries;
- changing read-query freshness or introducing long-lived leases.
