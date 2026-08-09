# Organizations Scope Lifecycle Task

Status: complete
Date: 2026-08-05

## Goal

Add one product-neutral, in-process lifecycle facade for the complete state
owned by an organization scope. Products may use it to assemble an authorized
portable export and, separately, to close and destroy that scope without
teaching Organizations about tenant termination, legal cases, product roles,
or product orchestration.

## Ownership

Organizations owns:

- organization, membership, invitation, enrollment-link, and enrollment-claim
  persistence;
- one monotonic mutation revision and terminal scope tombstone;
- bounded typed export of those five business stores;
- late inbox/outbox suppression, dependency-safe destruction, resumable
  progress, and immutable payload-free completion proof.

Products own authorization, approval, frozen-scope evidence, export schemas,
personal-data classification, artifact storage, owner ordering, restore and
backup policy, and final completion semantics.

Auth, Tenancy, Access Control, Notifications, Files, Task Runtime, and product
modules remain separate owners. Closing Organizations must not delete their
state or infer their identifiers.

## Application Boundary

Expose `IOrganizationScopeLifecycle` from the Application package. It has no
HTTP or administration route and offers only:

1. an exact scope snapshot;
2. keyset-paged typed export against one expected revision; and
3. one bounded resumable destruction batch under an idempotent operation id.

The organization id is the scope key. The facade does not require Auth,
Tenancy, ambient HTTP scope, or a product callback.

## Export

Export is capped, deterministic, and typed by store:

1. organization;
2. memberships;
3. invitations;
4. enrollment links; and
5. enrollment claims.

Pages use stable id cursors and one selected mutation revision. A caller must
verify the final snapshot still has that revision before accepting its
artifact.

Invitation and enrollment token digests are credentials, not portable data,
and are never exported. Inbox/outbox rows are transport journals and are also
excluded. Creation fingerprints and last-mutation retry proofs are internal
control metadata rather than portable data and are excluded as well. Actor and
subject identifiers, recipient email, lifecycle state, versions, counts, and
timestamps remain explicit typed fields so a product can classify them rather
than receiving opaque JSON.

## Admission And Consistency

Add one scope-state row for every organization that mutates after migration.
An existing organization without a row is an open revision-zero scope. Each
successful unit of work advances the affected scope once, regardless of how
many owned rows it changes.

Tracked organization changes, domain outbox writes, inbox processing, natural
lifecycle work, retention, and message-journal cleanup participate in the same
revision boundary. A closed scope rejects ordinary writes and suppresses late
inbox and outbox claims.

The reusable Framework change is limited to symmetric protected outbox hooks:
modules may filter claim candidates and override processed-row cleanup. No
Organizations or product vocabulary belongs in Framework.

## Destruction

The first accepted destruction call closes the scope tombstone and records a
durable operation. Active outbox leases return `Busy`; pending or abandoned
transport rows may then be discarded because closure intentionally prevents
their business effects from regrowing the scope.

Each call removes at most one non-empty bounded batch in foreign-key-safe
order:

1. inbox messages;
2. outbox messages;
3. enrollment claims;
4. invitations;
5. enrollment links;
6. memberships; and
7. the organization root.

Empty stages advance without pretending a removal occurred. Exact replay
returns the same terminal receipt. Restart resumes the persisted stage and
rolling proof. A changed scope, revision, batch size, or operation id conflicts
or reports stale state rather than broadening the operation.

The terminal tombstone and payload-free receipt retain only the organization
scope coordinate plus operation, revision, count, batch,
proof-version/digest, and timestamp coordinates. They must contain no
organization name or slug, subject/actor id, email, token digest, membership
id, join-source id, or transport payload.

## Delivery Slices

1. [x] Add the task, application contract, state/progress/receipt domain model,
   persistence mappings, and registration.
2. [x] Advance revisions for tracked writes and all set-based or transport
   paths; fail closed after scope closure.
3. [x] Add deterministic five-store export and focused unit tests.
4. [x] Add resumable destruction, replay proof, and focused unit tests.
5. [x] Add SQL Server and PostgreSQL migrations and prove both models are
   drift-free.
6. [x] Run the complete fast Organizations suite once and one exact PostgreSQL
   lifecycle scenario at the coherent slice boundary.
7. [x] Let products adapt the generic facade under their own owner catalogues;
   do not place product policy in this repository.
8. [x] Bind operation and receipt rows to the retained scope state, add
   provider-neutral coordinate constraints, and make the closed tombstone and
   terminal receipt database-enforced immutable evidence.

## Evidence

Verified on 2026-08-05:

- complete fast Organizations suite: 177 passed;
- reusable-module boundary guard: passed;
- SQL Server and PostgreSQL pending-model-change checks: clean; and
- exact PostgreSQL lifecycle scenario: 1 passed in 9 seconds, covering
  migration, cleanup and retention revisions, typed export, independent-write
  concurrency, active-lease blocking, late-message suppression, restartable
  destruction, terminal retention, exact replay, immutable closed state, and
  append-only receipt protection.

Both provider models also declare the trigger-backed state and receipt tables
to EF Core. SQL Server therefore uses trigger-compatible DML for normal state
updates and terminal receipt inserts instead of an unsupported bare `OUTPUT`
clause.

## Acceptance

- one concurrent write cannot commit under a stale selected revision;
- late inbox handling and outbox claiming cannot recreate or publish a closed
  scope;
- export is bounded, ordered, typed, secret-free, and stable by revision;
- destruction is bounded, restart-safe, dependency-safe, tenant-isolated, and
  exactly replayable;
- only the tombstone and payload-free proof remain at completion;
- no reusable-module or product dependency is introduced;
- both provider models are drift-free and the exact PostgreSQL scenario proves
  migration, concurrency, suppression, resume, and terminal retention.

## Deferred

- product approval and legal policy;
- cross-module Organizations/Access Control/Tenancy ordering;
- product export-field catalogues and protected artifacts;
- backup expiry and restore-readiness orchestration;
- a public deletion endpoint; and
- generalizing the lifecycle contract into Framework before another module
  proves the same semantic model.
