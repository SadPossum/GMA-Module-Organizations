# Organizations Module

`Gma.Modules.Organizations` owns a global organization catalog and the lifecycle of memberships, invitations, enrollment links, and approval-required join requests.

Organizations is intentionally independent from Auth, Tenancy, AccessControl, Notifications, and product modules. Authenticated subject ids are opaque strings. Cross-module activation, grant provisioning, and delivery behavior belongs in opt-in extensions.

Product orchestration may suspend, restore, or remove an exact ordinary membership through the Contracts-only `IOrganizationMembershipLifecycle`. The operation is idempotent, protects owner memberships, preserves normal optimistic concurrency and events, and reports stable no-op/not-found/protected outcomes. The facade owns no employment state, access-profile name, property plan, or product reason vocabulary.

Product orchestration may read exact sources, list sources, and deny invitation/enrollment sources through the Contracts-only `IOrganizationJoinSourceManager`. Reads authorize the owner before source state is loaded, and mutations reuse the owner-authorized CQRS paths. Secret-bearing replacement is deliberately excluded: products revoke or disable the prior source, then use caller-id-based `IOrganizationJoinSourceIssuer` so retries cannot duplicate a replacement source and plaintext tokens are never replayed.

Composed modules that already know an organization, enrollment link, and
subject may reconcile the retained authoritative claim through the
Contracts-only `IOrganizationEnrollmentClaimInspector`. It is an exact,
read-only lookup rather than a claim search or public authorization surface.

The organization id is the immutable technical scope id. A mutable slug is only a routing and display aid. Membership proves belonging but grants no product permission by itself.

Composed products may register one or more
`IOrganizationMutationAdmissionPolicy` implementations to guard owner-facing
organization profile, lifecycle, ownership, invitation, and enrollment-source
mutations. Policies compose fail-closed, remain optional for existing hosts,
and do not intercept defensive source denial or administration recovery.

Implementation direction and acceptance criteria are tracked in [Organizations Task](organizations-task.md).
Retry-safe self-service creation is tracked in
[Organizations Creation Idempotency Task](organizations-creation-idempotency-task.md).
The remaining production hardening work is tracked in
[Organizations Production Hardening Task](organizations-production-hardening-task.md).
The reusable host policy seam for owner-facing membership changes is tracked in
[Organizations Membership Change Policy Task](organizations-membership-change-policy-task.md).
The reusable admission seam for organization and join-source mutations is
tracked in
[Organizations Mutation Admission Policy Task](organizations-mutation-admission-policy-task.md).
The Contracts facade for product-owned source management is tracked in
[Organizations Join Source Management Task](organizations-join-source-management-task.md).
Retry-safe invitation reissue and enrollment-link rotation are tracked in
[Organizations Join Source Replacement Retry Safety Task](organizations-join-source-replacement-retry-safety-task.md).
The idempotent Contracts facade for issuing new invitation and enrollment
sources is tracked in
[Organizations Join Source Issuance Task](organizations-join-source-issuance-task.md).
Concurrent issuance and caller-owned HTTP retry semantics are tracked in
[Organizations Join Source Retry Safety Task](organizations-join-source-retry-safety-task.md).
Replacement-source lifecycle consistency is tracked in
[Organizations Lifecycle Consistency Task](organizations-lifecycle-consistency-task.md).
Transaction-stable lifecycle and membership authorization is tracked in
[Organizations Governance Transaction Consistency Task](organizations-governance-transaction-consistency-task.md).
Single-winner coordination across invitation and enrollment paths is tracked in
[Organizations Join-Subject Consistency Task](organizations-join-subject-consistency-task.md).
Terminal approval and rejection replay semantics are tracked in
[Organizations Join-Request Resolution Retry Safety Task](organizations-join-request-resolution-retry-safety-task.md).
Cross-provider ordinal storage for case-preserving subject and actor identifiers
is tracked in
[Organizations Ordinal Identity Storage Task](organizations-ordinal-identity-storage-task.md).

The bounded application-port filter for offline workflows that already hold a
candidate set is tracked in
[Organizations Access Candidate Filter Task](organizations-access-candidate-filter-task.md).

The durable natural-expiry lifecycle for invitations, enrollment links, and
pending join requests is tracked in
[Organizations Natural Expiry Task](organizations-natural-expiry-task.md).

The exact enrollment-claim reconciliation contract is tracked in
[Organizations Enrollment Claim Inspection Task](organizations-enrollment-claim-inspection-task.md).

The product-neutral complete-scope export and destruction facade is tracked in
[Organizations Scope Lifecycle Task](organizations-scope-lifecycle-task.md).

## Owned behavior

- organizations and immutable organization-to-scope identity;
- owner/member governance with last-active-owner protection;
- single-use invitations, optional recipient binding, revocation, and reissue;
- bounded shared enrollment links with automatic or approval-required admission;
- global administration inspection and recovery.

The module does not own user accounts, tenant-header resolution, product roles, employment profiles, notification delivery, or redirect URLs. Those integrations belong in hosts or opt-in GMA Extensions packages.

## HTTP surfaces

Authenticated organization operations live under `/api/organizations`. Invitation preview/acceptance lives under `/api/organization-invitations`; shared-link preview/claim lives under `/api/organization-enrollment`. Preview operations are `POST` requests with token bodies so bearer-like secrets are not copied into backend query logs.

Invitation and enrollment-link creation requires a caller-owned `sourceId`.
Exact retries return the existing source with an `already-issued` outcome and no
token; plaintext tokens are returned only by the transaction that creates the
source.

Invitation reissue and enrollment-link rotation likewise require a caller-owned
replacement source id. A committed replacement records non-secret predecessor
lineage so a lost response can be reconciled without replaying its token.

The administration surface lives under `/api/admin/organizations` and uses global `organizations.read` and `organizations.manage` permissions. Destructive lifecycle and owner-recovery operations require explicit confirmation.

## Configuration

```json
{
  "Organizations": {
    "SelfServiceCreationEnabled": false,
    "InvitationDefaultLifetimeHours": 168,
    "InvitationMaxLifetimeHours": 720,
    "EnrollmentDefaultLifetimeHours": 24,
    "EnrollmentMaxLifetimeHours": 720,
    "EnrollmentClaimLifetimeHours": 168,
    "EnrollmentMaxClaims": 1000,
    "Lifecycle": {
      "Enabled": false,
      "BatchSize": 100,
      "MaxBatchesPerCategoryPerCycle": 4,
      "IntervalMinutes": 5
    },
    "Retention": {
      "Enabled": false,
      "InvitationHistoryDays": 90,
      "EnrollmentHistoryDays": 90,
      "BatchSize": 500,
      "MaxBatchesPerCategoryPerCycle": 4,
      "IntervalMinutes": 60
    }
  }
}
```

Recipient-bound invitations fail closed unless a host or extension replaces `IOrganizationInvitationAdmissionPolicy` with verified-email logic. Unbound invitations and enrollment links never create owners.

## Operations

Token plaintext is returned once at issuance; only purpose-separated SHA-256 digests are persisted. All mutable aggregates carry optimistic versions, persistence uses a durable outbox, and PostgreSQL container tests prove bounded-claim concurrency and organization/subject isolation.

Transactional mutations that rely on organization lifecycle or membership
authority use a per-organization governance fence. Ordinary work shares the
fence; lifecycle, ownership, role, and membership-state changes take it
exclusively. This preserves concurrency between organizations and ordinary
operations while ensuring a command commits before a governance change or
re-authorizes against the change after it commits.

Membership-creating invitation and enrollment paths additionally use a
per-organization, per-subject transaction fence. An exact source replay remains
idempotent, while one current pending enrollment request wins over competing
links or invitation acceptance without consuming another source or reserving
more capacity. Unrelated subjects continue concurrently.

Approval-required join decisions are retry-safe after a committed response is
lost. The same decision with the immediately preceding claim version returns
the persisted terminal outcome after current resolver authorization. Approval
replay requires the exact active correlated membership and never reruns product
admission; rejection replay never releases capacity twice.

Self-service organization creation requires a caller-owned non-empty operation
id. Keep that id for retries of the same normalized name and slug; exact
replays return the current organization and active initial membership, while
changed reuse fails with `Organizations.CreationOperationConflict`.

Natural lifecycle processing and domain retention are disabled by default. Enable
both deliberately on one host responsible for Organizations maintenance.
Lifecycle processing durably expires due invitations, enrollment links, and
pending join requests before cleanup. Retention is bounded and removes only old
persisted terminal invitations, resolved claims whose parent link is terminal,
and terminal links with no remaining claims. Organizations, memberships, active
sources, pending requests, and message-journal recovery records are preserved.

Run `eng/verify.ps1` for boundaries, build, migration drift, unit tests, vulnerability audit, and PostgreSQL tests. Use `-SkipDocker` only when container tests are intentionally unavailable.
