# Organizations Scope Lifecycle Contract Boundary Task

Status: in progress
Date: 2026-08-10

## Goal

Expose the existing product-neutral organization scope snapshot, export, and
destruction capability through Organizations Contracts so product integrations
do not reference Organizations Application internals. Preserve the established
revision, paging, replay, proof, and destruction behavior.

## Audit Finding

`IOrganizationScopeLifecycle` and all of its request, result, status, progress,
receipt, and typed export records live in one Application port file. BunkFy's
Data Rights Organizations extension references Organizations Application only
to consume that cross-module capability.

The placement no longer matches the architecture proven by actual consumers.
Equivalent Access Control and Task Runtime lifecycle capabilities already live
in their modules' Contracts packages. The lifecycle implementation, storage
model, provider mappings, mutation revision, export protocol, and destruction
protocol are otherwise well covered and do not need redesign in this slice.

## Contract Decision

1. Organizations Contracts owns `IOrganizationScopeLifecycle` and every type
   appearing in that interface. The Application port file is removed after all
   in-repository consumers migrate.
2. Keep one lifecycle facade. Snapshot selection, stable export, and resumable
   destruction form one coordinated high-authority protocol, matching the
   established Access Control lifecycle contract. Products still own who may
   invoke it and in what orchestration order.
3. Split the contract into one public type per file. This removes the current
   oversized port file and aligns Organizations with the neighboring GMA
   lifecycle contracts.
4. Public lifecycle enums use an explicit `Unknown = 0` sentinel and strict,
   stable kebab-case JSON names. Unknown, numeric, undefined, and future values
   fail closed rather than being confused with a deliberate `Invalid` result.
5. Organizations Persistence implements and registers the Contracts interface
   directly. Lifecycle limits retain their current values, so database check
   constraints and provider models do not change.
6. BunkFy Data Rights Organizations references Organizations Contracts only.
   Its existing default handling continues to reject unknown or malformed
   lifecycle responses, and a scoped architecture guard prevents regression.
7. No Framework or GMA Extensions change is needed. This vocabulary and
   capability are owned by Organizations; BunkFy owns termination approval,
   frozen-scope evidence, export artifacts, retries, and final case semantics.

## Security And Privacy

- Moving the capability does not add an HTTP or administration endpoint.
- The service remains an in-process privileged capability composed by the
  host; product authorization and workspace termination fencing remain
  mandatory at the BunkFy boundary.
- Typed export records retain the existing explicit personal-data fields and
  continue to exclude token digests, transport payloads, creation
  fingerprints, and mutation retry proofs.
- Destruction progress and receipts remain payload-free and idempotent. No
  new logging or identifier disclosure is introduced.

## Delivery

- [x] Add split lifecycle Contracts and strict enum serialization coverage.
- [x] Move Organizations Persistence and tests to the Contracts namespace;
  remove the Application port types.
- [x] Move BunkFy Data Rights Organizations to Contracts only and add a scoped
  architecture guard.
- [ ] Update canonical Skeleton and product pins after focused verification.
- [ ] Run one consolidated non-Docker gate per changed repository at the
  completed slice boundary, publish exact pins, and verify exact CI.

## Verification Plan

- Use focused Contracts serialization, lifecycle persistence, Data Rights
  contributor, architecture, and Worker composition tests while editing.
- Run no Docker/provider gate because the persistence model, migration,
  generated SQL shape, transaction boundaries, limits, and provider behavior
  do not change.
- At the coherent slice boundary, run the complete non-Docker Organizations,
  Skeleton, and BunkFy backend gates once, plus the BunkFy root lightweight
  gate before publication.

## Not In This Slice

- redesigning export fields, lifecycle revisions, destruction stages, proofs,
  batching, receipts, or retry semantics;
- changing BunkFy termination authorization, fencing, owner ordering, artifact
  policy, or completion semantics;
- moving the Notifications lifecycle port, which belongs to a later
  Notifications-domain slice;
- adding lifecycle HTTP or administration APIs; or
- generalizing scope lifecycle vocabulary into Framework.
