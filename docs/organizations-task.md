# Organizations Module Task

Status: in progress
Date: 2026-07-14

## Goal

Provide reusable, production-shaped organization governance without absorbing identity, tenant resolution, product permission, employment, or delivery concerns.

## Delivery Slices

1. Establish the standalone package, global catalog persistence, organization lifecycle, membership discovery, owner governance, events, API, admin recovery, migrations, and concurrency proof.
2. Add recipient-bound single-use invitations with purpose-separated token digests, expiry, revocation, reissue, idempotent acceptance, and race proof.
3. Add reusable enrollment links, bounded claim counts, optional approval requests, rotation/disable behavior, and race proof.
4. Publish the source repository, mount it in composition repositories, and verify extension-only integration boundaries.

## Invariants

- organization ids are immutable normalized scope ids and slugs never become partition keys;
- one current membership exists per organization and subject;
- every active organization has at least one active owner;
- normal invitation and enrollment flows cannot create owners;
- suspension and removal deny membership immediately;
- token plaintext is returned only at issuance and is never persisted or emitted in events;
- exact retries by the same subject are idempotent while competing claims have one winner;
- all mutations use optimistic concurrency and durable outbox publication;
- list queries are indexed, paginated, and authorized in Organizations rather than through ambient scope filters.

## Completion Criterion

The module is complete when public and administration surfaces cover organization, membership, invitation, enrollment, and join-request lifecycle; SQL Server and PostgreSQL migrations are drift-free; real PostgreSQL tests prove race and isolation behavior; package, vulnerability, architecture, and source-boundary checks pass; and no implementation project references another reusable module.
