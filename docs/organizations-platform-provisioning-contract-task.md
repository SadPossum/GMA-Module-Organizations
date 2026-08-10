# Organizations Platform Provisioning Contract Task

Status: complete
Date: 2026-08-10

## Goal

Expose one product-neutral, retry-safe Organizations Contracts capability for a
trusted product orchestrator to create an organization and its initial owner as
part of a larger product-provisioning workflow. Keep self-service creation and
all product lifecycle policy outside this seam.

## Problem

Product onboarding may need to establish an Organizations scope after its own
authorization, commercial, or operational checks have succeeded. Calling the
self-service HTTP surface is the wrong boundary: that surface is end-user
policy, may be disabled, and cannot safely express a trusted orchestration retry
after the original response is lost.

A generic administration endpoint would be broader still. It would expose a
high-authority primitive without the product transaction, durable binding, or
reconciliation state that makes the resulting organization usable. The module
therefore needs a narrow in-process contract whose caller supplies a durable
identity and remains responsible for the surrounding workflow.

## Contract Decision

1. Organizations Contracts exposes `IOrganizationProvisioner`,
   `OrganizationProvisioningRequest`, and a bounded typed result. The
   implementation remains inside Organizations Application.
2. The capability is registered only when a composition root explicitly calls
   `AddOrganizationProvisioning()`. `AddOrganizationsApplication()` alone does
   not grant it. Hosts should opt in only where trusted product orchestration
   runs.
3. No HTTP, generic administration API, or administration CLI command is added.
   A product may expose its own authorized workflow, but it must not forward
   this contract as an unguarded organization-creation endpoint.
4. The caller owns a non-empty `OrganizationId` for the logical provisioning
   attempt. Organizations uses it as the immutable organization and scope id
   and serializes concurrent attempts with the existing provider-neutral
   transaction lock.
5. A fresh valid attempt creates the organization, initial owner membership,
   scope state, events, and outbox records atomically. It returns `Provisioned`
   with the committed organization and membership summary.
6. Trusted request identity consists of the normalized name, slug, and initial
   owner subject. Its fingerprint uses a provisioning-specific namespace,
   distinct from self-service creation. An exact retry returns
   `AlreadyProvisioned` and the current organization and existing initial-owner
   membership without another mutation or event.
7. `ActorId` is required provenance for the fresh mutation but is not part of
   trusted request identity. A retry may be recovered by a different authorized
   operator or service actor. The first successful actor remains the persisted
   creator; replay never rewrites provenance.
8. Reusing an organization id with different normalized intent, crossing from
   the self-service fingerprint namespace, encountering creation state that
   cannot prove the same request, or attempting to reuse a destroyed scope
   returns `IdentityConflict`. The module never resurrects a scope tombstone.
9. An exact trusted retry requires the original owner's membership to still
   exist, but it may return that membership in its current inactive state. The
   replay does not reactivate, restore, promote, or otherwise repair membership.
10. A slug owned by another organization returns `SlugConflict`. Invalid ids or
    values return `InvalidRequest`. Only `Provisioned` and
    `AlreadyProvisioned` are successful outcomes, and both carry a summary.
11. The seam bypasses `SelfServiceCreationEnabled` and self-service product
    admission because the outer orchestrator is the trust boundary. Existing
    self-service option gates, admission policies, actor-sensitive fingerprint,
    HTTP behavior, and active-membership replay requirement remain unchanged.
12. The implementation reuses the current organization row, immutable creation
    fingerprint, scope state, transaction boundary, and outbox. It adds no
    table, column, migration, receipt store, or generic response cache.

## Authorization And Provenance

`IOrganizationProvisioner` is a capability, not an authorization service. The
caller must freshly authenticate and authorize every invocation, including a
lost-response retry. Authorization may depend on product ownership, commercial
state, operator assurance, or workflow state; none of that vocabulary belongs
in Organizations.

The caller must also retain the product audit trail that explains who or what
authorized provisioning and why. `ActorId` identifies the opaque actor applied
to a fresh Organizations mutation. Because it is intentionally excluded from
the immutable retry fingerprint, it cannot substitute for a caller-side
authorization or audit record.

## Consumer Obligations

- Generate and durably persist the organization id before the first call. When
  the product has its own aggregate id, persist an explicit association instead
  of relying on a mutable slug or display name.
- Reuse that id only for the same normalized organization name, slug, and
  initial owner. Preserve the complete request across transport retries and
  process restarts.
- Resolve the intended owner subject before calling. A missing membership on a
  later replay is a reconciliation conflict, not permission to create another
  owner or another organization.
- Treat `Provisioned` and `AlreadyProvisioned` identically when advancing the
  product workflow. Persist the returned organization and membership identity
  through a local transaction, outbox, or another durable retryable step so a
  committed Organizations scope cannot be silently orphaned.
- Inspect the returned current membership state before declaring the product
  tenant operational. An inactive replay is truthful recovery evidence, not an
  instruction for Organizations to repair access.
- Treat `IdentityConflict` as a permanent reconciliation condition requiring
  investigation. Resolve `SlugConflict` through product naming policy and fix
  `InvalidRequest`; blind retries cannot change those outcomes.
- Compose the capability only in trusted orchestration hosts. Downstream
  modules should depend on Organizations Contracts, while product endpoints and
  workers own their own authorization, throttling, audit, and recovery policy.
- Continue downstream provisioning idempotently. Creating an organization does
  not create product profiles, property records, access grants, subscriptions,
  notifications, or readiness evidence.

## Ownership

- Organizations owns normalization, immutable provisioning identity,
  concurrency, organization-and-owner atomicity, tombstone enforcement, current
  replay summaries, and stable outcome vocabulary.
- The product owns authorization, provenance context, product-to-organization
  binding, workflow state, compensation or reconciliation, downstream module
  provisioning, and the definition of operational readiness.
- GMA Framework needs no change. The seam uses existing CQRS transactions,
  provider-neutral locking, results, time, identity generation, events, and
  outbox infrastructure.

## Delivery

- [x] Add the Contracts request, result, strict outcome serialization, and
  Contracts-only interface.
- [x] Share the existing creation transaction workflow without changing
  self-service policy or replay semantics.
- [x] Add provisioning-specific immutable identity, actor-independent recovery,
  inactive-membership replay, and scope-tombstone handling.
- [x] Make the privileged facade an explicit composition-root opt-in and keep it
  out of HTTP and administration surfaces.
- [x] Complete boundary, build, migration-drift, unit, and PostgreSQL
  concurrency verification on the final branch state.

## Verification

- Solution synchronization and Organizations boundary checks pass.
- The complete solution builds with zero warnings and zero errors.
- SQL Server and PostgreSQL report no pending model changes.
- All 329 unit and contract tests pass.
- All 15 Docker integration tests pass against PostgreSQL, including exact,
  divergent, same-slug, cross-channel, inactive-membership, live-row closure,
  tombstone, and no-duplicate-outbox provisioning cases.
- The transitive package vulnerability scan reports no vulnerable packages.

## Not In This Slice

- adding a generic administration or provisioning endpoint;
- changing self-service creation, product admission policies, or public HTTP
  responses;
- creating product tenants, properties, staff profiles, access grants,
  subscriptions, notifications, or other module state;
- automatic membership repair, owner replacement, or scope resurrection;
- a cross-module distributed transaction, saga framework, or generic
  idempotency store; or
- changing the Organizations persistence schema.
