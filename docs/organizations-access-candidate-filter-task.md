# Organizations Access Candidate Filter Task

Status: implemented and verified

## Goal

Provide a bounded, provider-neutral way for offline workflows to intersect an
already selected set of subject ids with authoritative active organization
access. This closes the gap between eventually consistent product projections
and membership revocation without teaching Organizations about notifications,
jobs, products, or audience policy.

## Contract

`IOrganizationAccessCandidateFilter` belongs in the Organizations application
ports. It accepts an organization id and no more than 500 candidate subject ids,
normalizes and deduplicates them, and returns only subjects whose organization
and membership are both active.

The contract deliberately does not list an organization's membership. Callers
must already have a legitimate candidate set. The persistence implementation
performs one bounded query and returns a stable ordinal ordering.

## Invariants

- Empty candidate requests return no subjects without querying; malformed or
  oversized requests fail before querying.
- Missing, suspended, or archived organizations return no allowed subjects.
- Missing, suspended, or removed memberships return no allowed subject.
- Duplicate candidates never duplicate output.
- Cancellation and database failures propagate so durable callers retry instead
  of silently treating an unavailable authority as successful delivery.
- No product role, property, notification, or recipient-selection vocabulary
  enters the reusable module.

## Verification

- focused in-memory persistence tests cover active and inactive combinations,
  normalization, ordering, empty input, malformed ids, and the size bound;
- PostgreSQL integration coverage proves the translated bounded query;
- dependency injection exposes the filter through the application port;
- the complete Organizations verifier remains green before publication.

Completed evidence:

- the complete Organizations boundary check and warning-free build pass;
- PostgreSQL and SQL Server migration models have zero drift;
- all 68 unit tests pass, including normalization, ordering, inactive access,
  empty input, malformed input, and the candidate bound;
- all four PostgreSQL integration tests pass, including the one-query filter
  before and after membership revocation;
- the complete direct and transitive package vulnerability audit reports no
  known vulnerable packages.
