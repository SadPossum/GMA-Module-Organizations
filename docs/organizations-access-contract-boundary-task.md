# Organizations Access Contract Boundary Task

Status: complete
Date: 2026-08-10
Completed: 2026-08-10

## Goal

Expose authoritative organization-access decisions and bounded candidate
intersection through Organizations Contracts so opt-in GMA extensions and
product integrations do not reference Organizations Application internals.
Preserve deliberate access denial while reporting unavailable authority
truthfully.

## Audit Finding

`IOrganizationAccessDecisionReader`, `OrganizationAccessDecision`, and
`IOrganizationAccessCandidateFilter` currently live in Organizations
Application ports even though they are consumed by GMA's
Organizations-Tenancy extension and BunkFy's Operations-Notifications
extension. This makes both integrations depend on the module implementation
layer.

The tenant adapter also maps every non-allowed enum value, including the
default `Unknown`, to the same HTTP 403 as a deliberate organization or
membership denial. Reader failures escape as an unclassified HTTP 500. The
single-subject reader does not validate its public inputs as strictly as the
bounded candidate filter.

The existing candidate-filter query, bounded input, fail-closed membership
semantics, stable ordering, and propagated failure behavior are otherwise
sound and require no persistence-model change.

## Decision Contract

1. Organizations Contracts owns the two access interfaces, their shared
   candidate limit, the access-decision enum, stable wire names, and strict
   JSON conversion. The legacy Application port types are removed after every
   in-repository consumer migrates.
2. `Allowed`, `OrganizationNotFound`, `OrganizationInactive`,
   `MembershipNotFound`, and `MembershipInactive` remain authoritative domain
   outcomes. `Unavailable`, `Unknown`, and undefined values are not deliberate
   denial.
3. Organizations Persistence implements both Contracts interfaces directly.
   Empty organization ids, malformed subject ids, null candidate sets, and
   requests above 500 candidates fail before querying. Database failures and
   caller cancellation continue to propagate from the module capability.
4. The Organizations-Tenancy extension preserves generic HTTP 401 and 403
   responses without revealing whether an organization or membership exists.
   `Unavailable`, `Unknown`, undefined decisions, and non-cancellation reader
   failures produce one stable generic HTTP 503 response. Caller-requested
   cancellation propagates.
5. The tenancy adapter logs only the reader type and failure category. It does
   not log organization ids, tenant ids, subject ids, headers, or membership
   state.
6. The bounded candidate filter still returns only an intersection of a
   caller-provided set. It never becomes a membership directory. Operational
   notification processing continues to propagate authority failures so its
   durable handler retries rather than silently dropping recipients.
7. GMA Framework needs no change. The decision vocabulary and authoritative
   reader are Organizations-owned capabilities; HTTP mapping belongs in the
   optional Organizations-Tenancy extension.

## Ownership

- Organizations Contracts owns the provider-neutral capability and decision
  vocabulary; Organizations Persistence owns the authoritative query and
  input normalization.
- GMA Extensions owns tenant-endpoint HTTP admission and maps Organizations
  outcomes to generic access responses.
- BunkFy Operations Notifications owns audience selection, product permission
  checks, delivery retry, and Staff correlation. It consumes only the bounded
  Organizations Contracts filter.

## Delivery

- [x] Add strict access Contracts, serialization coverage, and public-input
  guards; remove the Application port types.
- [x] Move GMA Organizations-Tenancy to Contracts only and cover denial,
  unavailability, exception, cancellation, bypass, and privacy behavior.
- [x] Move BunkFy Operations Notifications to Contracts only and add a scoped
  architecture guard.
- [x] Update canonical Skeleton and product pins after focused verification.
- [x] Run one consolidated non-Docker gate per changed repository at the
  completed slice boundary, publish exact pins, and verify exact CI.

## Verification Plan

- Use focused Contracts, persistence-reader, tenancy-extension, operational
  notification, and architecture tests while editing.
- Run no Docker/provider gate because the SQL query, model, migrations,
  transaction boundaries, and provider behavior do not change.
- At the coherent slice boundary, run the complete non-Docker Organizations,
  GMA-Extensions, Skeleton, and BunkFy backend gates once, plus the BunkFy root
  lightweight gate before publication.

## Focused Verification Evidence

- Organizations boundary guard passed; focused contract and access-reader
  selector passed 54 tests; the PostgreSQL integration project builds with
  zero warnings and errors.
- GMA Extensions boundary guard passed; all 17 Organizations-Tenancy tests
  cover active access, generic denial, indeterminate and failed authority,
  caller and provider cancellation, trusted bypass, and minimal composition.
- BunkFy Operations Notifications passed all 100 focused tests, including
  bounded batches and propagated authority failure; its Contracts-only
  architecture guard and Worker composition test each passed.

## Closure Evidence

- Organizations `0bddb6c2569cd8a4a2c26fab0265007b788058b5` passed the
  consolidated non-Docker verifier with solution synchronization, a
  zero-warning build, PostgreSQL and SQL Server migration drift checks, 313
  tests, and package audit. Exact GitHub runs `31348014245` (Validate) and
  `31348014259` (Security Baseline) passed.
- GMA Extensions `557ab7dd946304988d247fc597ba433b81053128` passed its
  CI-equivalent non-Docker gate with solution synchronization, boundary
  checks, a zero-warning build, 37 tests, and package audit. Exact GitHub runs
  `31347838376` (Validate) and `31347838355` (Security Baseline) passed.
- GMA Skeleton `7455a86ec4b516e7392d3fb91d34825caf86352c` passed its
  consolidated non-Docker verifier, including composed build, migration drift,
  module, architecture, integration, and release guards. Exact GitHub runs
  `31348277608` (Validate), `31348277626` (CodeQL), and `31348277623`
  (Security Baseline) passed.
- BunkFy Backend `34b3a08da07074535d3dce86569efc9f917c12a6` passed its
  consolidated non-Docker verifier with a zero-warning build, migration drift,
  all module and extension suites, 96 architecture tests, and 54 integration
  tests. Exact GitHub runs `31349391813` (Validate) and `31349391852`
  (Security Baseline) passed.
- BunkFy `ceb49fc722aef8ae97d174eb5e3922205b78d40e` passed the root
  lightweight composition, security, release, recovery, and deployed-probe
  policy gate. Exact GitHub runs `31350036266` (Validate), `31350036263`
  (CodeQL), and `31350036264` (Security Baseline) passed.
- No local Docker/provider gate was run because this slice changed no SQL query
  shape, persistence model, migration, transaction boundary, or provider
  behavior. Migration drift and the complete non-Docker composition gates were
  retained at the slice boundary.

## Not In This Slice

- moving `IOrganizationScopeLifecycle` or its export/destruction records to
  Contracts;
- changing membership, organization-lifecycle, tenancy-header, or bypass
  policy;
- adding membership enumeration, caching, retries, or a product audience API;
- changing notification recipient policy, authorization, or delivery; or
- adding a generic Framework access-decision pipeline.
