# Organizations HTTP State-Conflict Task

Status: complete
Date: 2026-08-09

## Goal

Keep the Organizations HTTP boundary honest when a syntactically valid
governance request cannot be applied because persisted organization or
membership state has moved on, or because an installed product policy rejects
the transition.

## Audit Finding

`ApiErrorStatusCodeMap` deliberately defaults unmapped errors to `400 Bad
Request`. Organizations maps optimistic-concurrency and several source-state
failures explicitly, but currently leaves organization lifecycle,
membership-state, and membership-policy conflicts unmapped. Public lifecycle
and membership handlers can return those errors, so clients receive `400` for
a request whose shape is valid and whose conflict must be resolved by
refreshing state or choosing another workflow.

## Contract

1. Malformed identifiers, names, slugs, actors, lifetimes, claim limits, and
   unsupported action values remain `400 Bad Request`.
2. Missing resources retain `404 Not Found`; missing authority retains `403
   Forbidden`; expired bearer-like sources retain `410 Gone`; temporary policy
   infrastructure failure retains `503 Service Unavailable`.
3. Current organization lifecycle, membership lifecycle, and installed
   membership-policy rejections map to `409 Conflict`.
4. Error codes and response bodies do not change. No Contracts, domain,
   persistence, migration, Framework, Skeleton-host, or product change is
   required.

## Delivery

- [x] Expose the membership-state domain errors through the application error
  catalog used by the API boundary.
- [x] Map all public organization, membership, and membership-policy state
  conflicts to HTTP 409.
- [x] Add a focused API guard covering the complete conflict set and retaining
  the 400 fallback for malformed input.
- [x] Run the Organizations fast gate once at the completed slice boundary,
  publish the module pin, then align canonical consumers.

## Implementation Evidence

Verified locally on 2026-08-09:

- Organizations boundary checks and solution synchronization pass;
- the solution builds with zero warnings and zero errors;
- SQL Server and PostgreSQL migration drift checks pass with no schema change;
- all 249 unit, contract, and architecture tests pass, including the complete
  public state-conflict set and the unchanged malformed-input fallback; and
- the package audit reports no known vulnerabilities.

The change is confined to the Organizations application error catalog, API
status map, focused API tests, and this task record. Framework behavior and
public error codes remain unchanged.

Publication evidence:

- Organizations implementation `941c223057d10b0d80ef1f6d43751098b846cf29`;
- Organizations synchronized solution pin
  `465d183b285a65fae592b620458a7f23800c9ac8`;
- canonical Skeleton consumer pin
  `a618860f5418f25c89221076c79dd399f005afef`;
- BunkFy backend consumer pin
  `03a3a09b53486703332f7721d173e46290be10ae`; and
- BunkFy root backend pin
  `bdb36372333787e83e0bf7619cc2aa1d1e482e88`.

## Not In This Slice

- changing authorization or resource-disclosure behavior;
- changing aggregate transition rules or retry semantics;
- adding new problem-details fields or public error codes;
- changing anonymous token-route caching or rate limiting; or
- introducing generic Framework error taxonomies.
