# Organizations Join Source Issuance Task

Status: completed
Date: 2026-07-21

## Goal

Let another module issue organization invitations and enrollment links through a narrow Contracts facade without referencing Organizations Application commands. Keep token security, owner authorization, source constraints, and source lifecycle inside Organizations.

## Delivery Slice

1. Add Contracts-only issuance requests, outcomes, and `IOrganizationJoinSourceIssuer`.
2. Use a caller-provided source id as the idempotency identity.
3. Return a plaintext token only for the call that creates the source; an exact replay returns source metadata without the token.
4. Reject reuse of a source id with different recipient, lifetime, claim, approval, subject, or actor inputs.
5. Keep existing HTTP creation endpoints backward compatible.
6. Prove exact replay, conflicting replay, authorization, constraints, and one-time secret behavior with focused tests.

## Ownership Boundaries

Organizations owns generic invitation and enrollment issuance, token digests, expiry and claim limits, owner authorization, and idempotency semantics. Consumers own any product plan bound to the returned source id.

The facade must contain no product role, property, Staff, tenant-plan, or workflow vocabulary. Cross-module consumers must persist their own intent before issuance and fail closed when product plan completion has not occurred.

## Acceptance Criteria

- consumers reference only Organizations Contracts;
- a successful first call returns one plaintext token that is never persisted;
- an exact replay does not create another source and never returns the token again;
- a mismatched replay fails with a stable conflict code;
- owner and active-organization checks are identical to the HTTP commands;
- existing API behavior and DTOs remain compatible;
- the standalone module verification gate passes on Windows and Linux.

## Verification

- Organizations boundary and PostgreSQL/SQL Server migration-drift checks passed.
- The solution built with zero warnings.
- 61 unit tests and 4 Docker integration tests passed.
- NuGet vulnerability checks reported no vulnerable packages.
