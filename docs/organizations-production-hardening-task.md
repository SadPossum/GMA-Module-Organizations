# Organizations Production Hardening Task

Status: completed
Date: 2026-07-19
Completed: 2026-07-19

## Goal

Close the remaining production gaps in the reusable Organizations domain without moving identity, tenancy, product authorization, staff profiles, notification delivery, or product onboarding into the module.

The existing organization, membership, invitation, enrollment, administration, persistence, and concurrency model remains the foundation. This slice hardens secret handling and bounded data lifecycle behavior, then proves the canonical host composition in GMA Skeleton and BunkFy.

## Audit Baseline

- the standalone boundary check passes and no Organizations implementation project references another reusable module;
- SQL Server and PostgreSQL migrations are drift-free;
- the zero-warning build, 31 unit tests, vulnerability audit, and 3 PostgreSQL integration tests pass;
- invitation and enrollment tokens are 256-bit random values stored only as purpose-separated SHA-256 digests;
- public and administration surfaces cover the current organization, membership, invitation, enrollment-link, and join-request lifecycle;
- Auth recipient verification and Organizations-to-Tenancy admission are correctly isolated in opt-in GMA Extensions;
- BunkFy-specific access roles and Staff projections remain outside GMA.

## Findings

1. Token issuance, reissue, rotation, preview, acceptance, and claim responses do not consistently emit `Cache-Control: no-store` and `Pragma: no-cache`.
2. Expired and safely terminal invitation and enrollment artifacts have no bounded, opt-in retention policy. Long-running installations therefore retain recipient email addresses, subject ids, and unusable token digests indefinitely.
3. The canonical Skeleton host and generated source-first hosts do not classify the anonymous invitation and enrollment token routes as sensitive rate-limit paths.
4. The original task remains marked in progress even though its core delivery slices and completion gates pass; it needs an explicit completion record after this hardening slice is verified.

## Delivery Slices

### 1. Sensitive HTTP Responses

- apply no-store/no-cache response headers to every operation that returns a token or accepts a bearer-like invitation or enrollment token;
- cover successful and failed token operations so intermediaries cannot cache token-derived metadata or error responses;
- keep token values in request bodies and never add them to routes, query strings, logs, metrics, events, or exception messages;
- add focused endpoint-support tests that prove the response policy.

### 2. Bounded Domain Retention

- add `Organizations:Retention` options with explicit enablement, history windows, batch size, per-cycle batch bounds, and interval validation;
- run cleanup only when enabled and use the framework `BoundedBatchProcessor` so each iteration has a strict work ceiling;
- remove expired or terminal invitations only after the configured history window;
- remove accepted or rejected enrollment claims only after their parent link is expired, disabled, or rotated and past the configured history window;
- remove an expired, disabled, or rotated enrollment link only after it has no remaining claims;
- retain pending join requests and their parent links because silently discarding an unresolved governance decision would change domain behavior;
- never remove organizations, memberships, active links, pending invitations, pending join requests, outbox failures, or inbox deduplication records through this retention service;
- leave outbox/inbox retention to the existing module-owned message-journal cleanup contracts.

### 3. Persistence And Proof

- add provider-specific SQL Server and PostgreSQL migrations for any cleanup indexes required by the bounded queries;
- prove option validation, disabled-by-default registration, bounded cleanup behavior, parent/child ordering, and pending-request preservation;
- extend real PostgreSQL coverage for the retention queries and keep migration drift clean;
- retain the existing race, subject-isolation, and access-decision proofs.

### 4. Canonical Composition

- add `/api/organization-invitations` and `/api/organization-enrollment` to Skeleton's sensitive rate-limit paths;
- generate those paths only when Organizations is selected by `eng/new-gma-app.ps1`;
- add generator and architecture guards so Organizations-only and combined selections remain correct;
- document that retention is opt-in and should be enabled deliberately on the host responsible for Organizations maintenance;
- align BunkFy configuration and pins after upstream repositories are published, without changing BunkFy product-domain behavior.

## Ownership Boundaries

Organizations continues to own:

- organization identity and lifecycle;
- membership and owner invariants;
- invitation and enrollment governance;
- Organizations-owned persistence and retention.

GMA Extensions continues to own:

- Auth-backed recipient verification;
- Organizations-backed tenant admission.

Products continue to own:

- workspace/team terminology;
- custom roles and permission templates;
- staff or employee profiles and onboarding forms;
- QR/link presentation, redirect destinations, and notification content;
- product-specific restrictions such as limiting a subject to one workspace.

No Framework change is planned. The generic bounded-batch and message-journal primitives already cover the reusable infrastructure needed by this slice.

## Acceptance Criteria

- all token-sensitive HTTP operations emit no-store/no-cache headers;
- retention is disabled by default, validated on startup, bounded, provider-neutral, and safe under repeated or concurrent execution;
- pending join requests are preserved and terminal child records are removed before parent links;
- SQL Server and PostgreSQL migrations are drift-free;
- Organizations boundary, build, unit, vulnerability, and Docker verification pass;
- GMA Extensions tests prove the existing Auth and Tenancy bridges still compose without leaking dependencies into Organizations;
- Skeleton fast, migration, Docker, generator-selection, and cross-replica checks pass;
- BunkFy backend and web contract verification pass after pin alignment;
- the original Organizations task records completion only after all upstream and consumer checks are green.

## Explicitly Deferred

- custom product roles and policy editors;
- staff-profile data capture during workspace creation or invitation acceptance;
- invitation email/notification adapters and product redirect URLs;
- pending join-request expiry, withdrawal, or cancellation semantics, which require an explicit domain-state and integration-event design;
- organization deletion and legal erasure workflows;
- organization merge, hierarchy, billing ownership, or delegated administration.

## Completion Evidence

- Organizations implementation checkpoint `ee5d74d` passes the source-boundary and vulnerability checks, zero-warning build, SQL Server and PostgreSQL migration drift, 40 unit tests, and 4 real PostgreSQL integration tests; its exact GitHub validation and PostgreSQL jobs are green.
- GMA Extensions remains unchanged and passes all 19 Auth/Organizations, Organizations/Tenancy, and Auth/Notifications tests against the hardened module with no vulnerable packages.
- GMA Skeleton checkpoint `d3a6b98` emits Organizations settings and sensitive routes only for selected apps, passes the generated selection matrix, zero-warning build, all provider migration checks, the full fast suite, and 21 Docker integration tests; exact Linux and Windows CI jobs are green.
- BunkFy Backend checkpoint `9349f03` records the module pin, keeps retention disabled in API and Worker baselines, protects OpenAPI export from cleanup overrides, and passes the full build, migration, fast, and 32-test Docker gates. Its exact Linux, Windows, and Docker workflows are green.
- BunkFy root checkpoint `672b78d` records the backend pin and passes recursive composition, backend verification, current OpenAPI/generated web contracts, and frontend verification in its exact GitHub workflow.

No Framework or product-domain behavior was added. The deferred items remain separate future domain decisions rather than incomplete acceptance criteria for Organizations.
