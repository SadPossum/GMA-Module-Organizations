# Organizations Lifecycle Consistency Task

Status: completed
Date: 2026-07-19
Completed: 2026-07-19

## Goal

Keep replacement invitation and enrollment credentials aligned with organization lifecycle state without moving product onboarding, authorization roles, identity verification, or tenant resolution into Organizations.

## Audit Baseline

- the standalone boundary, zero-warning build, migration drift, package vulnerability, 40-unit-test, and 4-PostgreSQL-test gates pass;
- initial invitation and enrollment-link issuance requires an active organization;
- invitation acceptance, enrollment claims, and join-request approval require an active organization;
- invitation revocation and enrollment-link disablement remain useful defensive cleanup operations when an organization is inactive;
- reissuing an invitation and rotating an enrollment link currently mint replacement secrets without checking the owning organization state.

## Delivery Slice

1. Require the owning organization to exist and be active before an invitation is reissued.
2. Require the owning organization to exist and be active before an enrollment link is rotated.
3. Preserve revocation and disablement for inactive organizations so operators can close outstanding credentials safely.
4. Add focused application tests for the blocked replacement paths and the preserved cleanup paths.
5. Re-run the complete standalone module gate, then align and verify Extensions, Skeleton, and BunkFy against the published Organizations head.

## Ownership Boundaries

Organizations owns this rule because organization lifecycle and invitation/enrollment governance are all module-owned state. No Framework primitive or cross-module extension is required.

Products continue to own workspace terminology, staff profiles, product roles, invitation presentation, redirects, and onboarding forms. Auth-backed recipient verification and Organizations-backed tenant admission remain in GMA Extensions.

## Acceptance Criteria

- suspended and archived organizations cannot mint replacement invitation or enrollment secrets;
- inactive organizations can still revoke invitations and disable enrollment links;
- active behavior and token response contracts remain unchanged;
- no implementation project references another reusable module;
- SQL Server and PostgreSQL migrations remain drift-free;
- the complete Organizations verification gate passes;
- GMA Extensions, Skeleton, and BunkFy composition checks pass against the exact published Organizations head.

## Completion Evidence

- Organizations implementation `8f0ebc1` passes the complete local gate with 42 unit tests, 4 PostgreSQL tests, drift-free SQL Server and PostgreSQL migrations, boundary checks, a zero-warning build, and no vulnerable packages; GitHub Actions run `29695123950` is green.
- GMA Extensions remains unchanged at `abed85e`; all 19 extension tests pass locally against the hardened Organizations implementation with no vulnerable packages.
- GMA Skeleton `2043749` passes its complete local verification gate and GitHub Actions run `29695430202` on Windows and Ubuntu.
- BunkFy backend `68cf14a` passes its complete local verification gate and 32 Docker tests; GitHub Actions runs `29696225723` and `29696225733` are green.
- BunkFy root `6266355` passes the recursive submodule guard and clean-checkout GitHub Actions run `29696514844`, including backend verification and the web checks.
- No Framework, Extensions, or product behavior changed in this slice; ownership remains within Organizations.
