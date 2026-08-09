# Organizations Ordinal Identity Storage Task

Status: module implementation complete; composition verification pending
Date: 2026-08-09

## Goal

Keep case-preserving Organizations subject and actor identifiers distinct with
the same ordinal equality semantics on PostgreSQL and SQL Server.

## Audit Finding

`OrganizationSubjectId` and `OrganizationActorId` trim but do not change the
case of external identifiers. Application comparisons and join-subject lock
keys are ordinal, while PostgreSQL text equality is case-sensitive by default.
Organizations does not currently override SQL Server's database collation, so
a common case-insensitive SQL Server database can collapse or match identifiers
that the domain and lock coordinator treat as distinct.

This can make membership uniqueness, enrollment-claim uniqueness, exact
membership reads, and pending-request checks provider-dependent. Generic GMA
consumers cannot assume that every identity provider emits lowercase or GUID
subject values.

## Ownership

- Framework owns the provider-aware `UseOrdinalStringComparison` persistence
  primitive. It already maps SQL Server strings to
  `Latin1_General_100_BIN2` and leaves PostgreSQL defaults unchanged; no
  Framework change is required.
- Organizations owns which of its persisted values are opaque identifiers and
  applies the Framework primitive explicitly to those properties.
- Provider migration projects own the physical schema change. Only SQL Server
  requires a migration.
- Products own identity-provider selection and account policy. BunkFy concepts
  and subject normalization do not belong in this slice.

## Storage Contract

1. Subject and actor identifiers remain trimmed, case-preserving values.
   `Case-Subject` and `case-subject` are distinct identifiers.
2. PostgreSQL continues to use deterministic case-sensitive text equality.
3. SQL Server subject and actor columns use
   `Latin1_General_100_BIN2`, matching ordinal application comparisons without
   query-level collation or case conversion.
4. The contract applies to membership subjects, invitation inviter and
   accepted subjects, enrollment-link creator subjects, enrollment-claim
   subjects, and aggregate audit actor properties.
5. Normalized slugs and recipient emails keep their existing lowercase domain
   normalization. Canonical token and SHA-256 digests and GUID-derived scope
   ids are outside the opaque-identity convention.
6. The SQL Server migration drops and recreates every index that depends on an
   altered subject column. Existing values are preserved.
7. Indexed subject queries remain sargable: do not add query-level `COLLATE`,
   `ToLower`, or `ToUpper` operations.
8. Join-subject coordination continues to hash the exact normalized subject.
   Lock and database equality therefore agree on both supported providers.

## Delivery

- [x] Apply ordinal storage explicitly to every Organizations subject and actor
  property.
- [x] Add a SQL Server migration that safely rebuilds affected indexes around
  the collation changes.
- [x] Add design-model guardrails for the complete property set and assert that
  indexed queries do not inject query-level collation.
- [x] Add one SQL Server relational proof that case-distinct memberships and
  enrollment claims coexist and are read by exact identity.
- [ ] Run the completed-slice Organizations gate and one Docker invocation,
  then verify GMA Skeleton and BunkFy consumers before publication.

## Module Verification

- The boundary guard passes and the solution builds with zero warnings and
  zero errors.
- All 209 unit tests pass, including provider design-model coverage for all
  fourteen opaque identity properties and a sargable generated-query check.
- PostgreSQL and SQL Server migration models report no drift. PostgreSQL has no
  schema change; the SQL Server migration rebuilds all four dependent subject
  indexes around the collation changes.
- The transitive package audit reports no vulnerable packages.
- One focused SQL Server container invocation passes. It applies the complete
  migration chain and proves case-distinct memberships, enrollment claims, and
  actor queries remain isolated.

## Not In This Slice

- changing subject or actor validation and normalization;
- applying a global property-name convention in Framework;
- auditing or migrating Access Control or another module's subject storage;
- changing normalized email, slug, token, hash, or scope-id behavior;
- changing public Contracts, HTTP schemas, or BunkFy product policy.
