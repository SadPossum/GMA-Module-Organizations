# Organizations Truthful Pagination Task

Status: implemented and verified
Date: 2026-08-09

## Goal

Make every bounded Organizations directory report whether another page exists,
without exact count queries, redundant terminal probes, or product-specific
pagination behavior.

## Audit Finding

The organization, catalog, membership, invitation, enrollment-link, and pending
join-request list contracts expose `Page` and `PageSize` but not continuation.
An exactly full terminal page is therefore indistinguishable from a truncated
page. API, CLI, and product consumers must either guess from item count or issue
an avoidable empty-page request.

## Ownership

- Organizations owns ordering, bounded persistence reads, and truthful
  continuation for its six directories.
- GMA Framework needs no change. The existing normalized `PageRequest` already
  provides bounded page and page-size coordinates; continuation is part of each
  module-owned response contract.
- GMA Skeleton consumes the published Organizations contract through its module
  pin. Its administration API and CLI need no product-specific mapping.
- BunkFy Workspaces propagates Organizations continuation through its
  product-owned join-source facade. The web consumes the resulting fact for
  workspace-member and issued-source pagination.

## Contract

1. Add additive `HasMore` metadata to all six list responses while preserving
   existing item, page, and page-size fields.
2. Keep source compatibility for existing in-process consumers by defaulting
   constructor-level continuation to `false`; serialized API responses always
   expose the boolean.
3. Preserve every existing stable sort and fetch at most `PageSize + 1` rows.
4. Return at most `PageSize` items and set `HasMore` only when the bounded
   lookahead row exists.
5. Do not run exact counts, infer continuation from a full page, or issue a
   second query.
6. Empty pages and exactly full terminal pages report `HasMore = false`.

## Delivery

- [x] Extend all Organizations list contracts and repository queries.
- [x] Prove first-page continuation and terminal-page behavior for every
  directory, plus contract shape and provider translation.
- [x] Propagate join-source continuation through BunkFy Workspaces.
- [x] Use explicit continuation in the BunkFy members and issued-source pagers.
- [x] Regenerate OpenAPI/web contracts and prepare coordinated GMA Skeleton and
  BunkFy pin updates.
- [x] Run focused checks while editing, then one consolidated non-Docker gate
  and one focused PostgreSQL provider gate at the completed slice boundary.

## Verification

- All six repository directories prove first-page continuation, exactly-full
  terminal pages, empty pages, and bounded result sizes.
- The synchronized BunkFy backend gate passed source-package checks, a
  zero-warning build, migration drift for every provider, and all non-Docker
  suites, including 230 Organizations and 344 Workspaces tests.
- The focused PostgreSQL membership-discovery test passed without skipping and
  preserved subject and organization isolation while exercising lookahead.
- Web typecheck, lint, all 253 tests, production build, OpenAPI generation, and
  generated-contract drift checks passed.

## Not In This Slice

- cursor pagination or replacing the existing page-number contract;
- server-side search, exact totals, or page-number jump controls;
- changing directory authorization, filtering, or lifecycle semantics;
- adding a generic Framework page-response type;
- loading unbounded directories into a browser or CLI process.
