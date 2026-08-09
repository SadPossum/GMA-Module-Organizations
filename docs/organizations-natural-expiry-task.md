# Organizations Natural Expiry Task

Status: complete
Date: 2026-07-28

## Goal

Turn time-based invitation, enrollment-link, and pending join-request expiry
into durable Organizations-owned lifecycle transitions. Expiry must remain
generic, bounded, observable through the outbox, and safe under concurrent
maintenance hosts without moving product onboarding or retained profile data
into GMA.

## Audit Baseline

- invitation and enrollment-link reads already report an effective `Expired`
  status after their configured deadline;
- acceptance and claim commands reject expired sources synchronously;
- persisted invitation and link state remains `Pending` or `Active`, so no
  expiry integration fact is published;
- approval-required enrollment claims have no independent decision deadline and
  can retain subject ids indefinitely;
- retention may directly delete old time-expired sources without first
  publishing a durable lifecycle fact;
- existing changed-event v1 contracts intentionally cannot represent an
  `Expired` transition.

## Domain Decisions

### Source expiry

- A pending invitation becomes durably `Expired` at its invitation deadline.
- An active enrollment link becomes durably `Expired` at its link deadline.
- Source expiry prevents new admission but does not undo memberships or claims
  created while the source was valid.
- Disable, rotate, revoke, accept, and claim operations continue to enforce the
  deadline synchronously, even when the maintenance worker has not run yet.

### Pending claim expiry

- A submitted approval-required claim is its own governance item after a valid
  link claim.
- Each pending claim receives an explicit decision deadline when it is created.
- The default claim lifetime is seven days and is host-configurable within a
  bounded maximum.
- Link expiry, disablement, or rotation does not cancel an already submitted
  claim. An owner may approve or reject it until the claim deadline.
- A pending claim becomes durably `Expired` at its own deadline.
- Expiring a claim releases reserved capacity only while its source link still
  has a mutable capacity counter. Terminal links do not need a counter mutation.
- Accepted, rejected, and expired claims are terminal.

This separation keeps link issuance policy independent from review turnaround,
while ensuring unresolved subject identifiers do not remain pending forever.

## Event Compatibility

- Existing `*-changed` v1 integration contracts remain byte- and
  behavior-compatible.
- Expiry uses dedicated version-1 integration events for invitations,
  enrollment links, and enrollment claims.
- Expiry events contain only scope and aggregate identifiers, the aggregate
  version, the configured effective deadline, and the event occurrence time.
- Claim-expiry events do not repeat the subject id. Consumers correlate by the
  authoritative claim id.
- Domain transitions and outbox writes share the normal Organizations unit of
  work. Direct SQL status updates are forbidden.

## Runtime Model

- Add an opt-in `Organizations:Lifecycle` maintenance worker with a bounded
  batch size, bounded batches per category, and bounded interval.
- Lifecycle processing is disabled by default. A composed application enables
  it on one maintenance host, normally its worker process.
- Claims are processed before links so due pending decisions can release active
  link capacity before source terminalization.
- Optimistic concurrency and idempotent terminal checks make accidental
  multi-replica execution safe. Single-host ownership remains the recommended
  operating mode to avoid duplicate reads.
- Public reads continue to calculate effective expiry from timestamps so users
  do not observe a source or claim as usable during worker lag.

## Retention Ordering

1. The lifecycle worker persists expiry and writes the corresponding outbox
   event.
2. Normal outbox delivery exposes the terminal fact to consumers.
3. Retention removes only persisted terminal records after the configured
   history window.
4. Terminal claims are removed before their parent enrollment link.
5. A link is removed only after it has no claims.

Retention must no longer treat persisted `Pending` invitations or `Active`
links as deletable merely because their timestamp is old.

## Delivery Slices

### 1. Domain And Contracts

- add persisted expired states and aggregate transitions;
- add a decision deadline and expired state for pending claims;
- add compatibility-safe, payload-minimal expiry integration events;
- expose claim deadline and effective status through the existing DTO surface.

### 2. Application And Persistence

- enforce claim deadlines in owner decisions;
- exclude overdue claims from pending-review queries during worker lag;
- add bounded lifecycle commands, due-item queries, options, and hosted service;
- add the claim-deadline column and due-query index for SQL Server and
  PostgreSQL;
- align retention queries with persisted terminal state.

### 3. Composition And Consumers

- enable lifecycle processing only in the canonical Skeleton worker;
- update generated host configuration and architecture guards;
- publish Organizations and advance the Skeleton pointer;
- separately advance BunkFy GMA pointers and consume expiry facts in the
  Workspaces onboarding projection.

## Ownership Boundaries

Organizations owns source and claim lifecycle, deadlines, persistence, and
payload-free expiry facts.

Products own staff profiles, copied onboarding data, user-facing expiry copy,
notifications, redirects, and their reaction to authoritative expiry events.

GMA Extensions remains responsible only for optional cross-module policies.
No Framework change is planned because current CQRS unit-of-work, outbox,
bounded-batch, clock, and optimistic-concurrency primitives are sufficient.

## Acceptance Criteria

- every due source and pending claim has a durable, idempotent expiry path;
- claim approval cannot create membership after the claim deadline;
- a valid pending claim remains reviewable after its source link expires;
- existing changed-event v1 contracts and non-expiry behavior remain unchanged;
- expiry events contain no invitation recipient or claim subject data;
- lifecycle processing is bounded, opt-in, and safe under concurrent attempts;
- retention only deletes persisted terminal state in child-before-parent order;
- SQL Server and PostgreSQL migrations are drift-free;
- focused domain, contract, application, and lifecycle tests pass;
- one final PostgreSQL proof covers transition, outbox, concurrency, and
  retention ordering;
- Skeleton and BunkFy consume the published module in separate verified slices.

## Module Evidence

Completed on 2026-07-28:

- architecture and module-boundary checks pass;
- the full solution builds with zero warnings and zero errors;
- SQL Server and PostgreSQL migration drift checks pass;
- all 89 Organizations unit and contract tests pass;
- package vulnerability audit reports no known vulnerable dependencies;
- all five PostgreSQL integration scenarios are green, including concurrent
  expiry, claim-before-link processing, capacity release, persisted terminal
  state, and payload-minimal outbox facts.

## Composition Evidence

Composition is complete as of 2026-08-09:

- the canonical Skeleton exposes bounded lifecycle settings as inert by
  default, enables them only in its development worker, and verifies explicit
  worker ownership through architecture tests;
- BunkFy's worker is the single configured lifecycle owner and its host
  integration test verifies the hosted service and Workspaces subscriptions;
- Workspaces consumes invitation, enrollment-link, and enrollment-claim expiry
  facts through Contracts-only handlers, with focused unit and PostgreSQL
  persistence coverage; and
- production admission rejects lifecycle ownership on non-worker hosts and
  rejects ambiguous ownership.

Product onboarding status, retained profile data, recovery, and user-facing
copy remain in BunkFy. Organizations continues to own deadlines, terminal
state, maintenance transitions, and expiry facts.
