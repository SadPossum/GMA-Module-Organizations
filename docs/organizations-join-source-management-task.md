# Organizations Join Source Management Task

Status: implemented
Date: 2026-07-22

## Goal

Let composing products inspect and deny existing invitation/enrollment sources through Organizations Contracts without referencing Application internals or calling the module's HTTP API server-to-server.

## Boundary

- Organizations owns source status, owner authorization, optimistic versions, revocation/disable semantics, audit facts, and persistence.
- Products own their access plans, labels, resource selections, replacement workflow, and UI.
- The facade contains no product profile, property, employment, or redirect vocabulary.

## Delivery

- Add `IOrganizationJoinSourceManager` for owner-checked paged invitation and enrollment-link reads.
- Add deny-first invitation revocation and enrollment-link disable operations through the existing CQRS handlers.
- Return stable Contracts DTOs plus error codes and never expose stored token digests.
- Keep plaintext replacement secrets out of the manager. Products replace a source by disabling/revoking it and then using the idempotent caller-id `IOrganizationJoinSourceIssuer`; the non-idempotent HTTP reissue/rotate commands remain interactive front-door behavior.

## Verification

- the manager dispatches existing owner-authorized queries and commands;
- disable can never request a replacement token;
- expected failures preserve stable error codes and no value;
- null requests fail before dispatch;
- registration remains idempotent;
- boundaries, build, migration drift, unit tests, package audit, and PostgreSQL integration tests remain green.
