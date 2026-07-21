# Organizations Module

`Gma.Modules.Organizations` owns a global organization catalog and the lifecycle of memberships, invitations, enrollment links, and approval-required join requests.

Organizations is intentionally independent from Auth, Tenancy, AccessControl, Notifications, and product modules. Authenticated subject ids are opaque strings. Cross-module activation, grant provisioning, and delivery behavior belongs in opt-in extensions.

Product orchestration may suspend, restore, or remove an exact ordinary membership through the Contracts-only `IOrganizationMembershipLifecycle`. The operation is idempotent, protects owner memberships, preserves normal optimistic concurrency and events, and reports stable no-op/not-found/protected outcomes. The facade owns no employment state, access-profile name, property plan, or product reason vocabulary.

The organization id is the immutable technical scope id. A mutable slug is only a routing and display aid. Membership proves belonging but grants no product permission by itself.

Implementation direction and acceptance criteria are tracked in [Organizations Task](organizations-task.md).
The remaining production hardening work is tracked in
[Organizations Production Hardening Task](organizations-production-hardening-task.md).
The reusable host policy seam for owner-facing membership changes is tracked in
[Organizations Membership Change Policy Task](organizations-membership-change-policy-task.md).

## Owned behavior

- organizations and immutable organization-to-scope identity;
- owner/member governance with last-active-owner protection;
- single-use invitations, optional recipient binding, revocation, and reissue;
- bounded shared enrollment links with automatic or approval-required admission;
- global administration inspection and recovery.

The module does not own user accounts, tenant-header resolution, product roles, employment profiles, notification delivery, or redirect URLs. Those integrations belong in hosts or opt-in GMA Extensions packages.

## HTTP surfaces

Authenticated organization operations live under `/api/organizations`. Invitation preview/acceptance lives under `/api/organization-invitations`; shared-link preview/claim lives under `/api/organization-enrollment`. Preview operations are `POST` requests with token bodies so bearer-like secrets are not copied into backend query logs.

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
    "EnrollmentMaxClaims": 1000,
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

Domain retention is disabled by default and should be enabled deliberately on a host responsible for Organizations maintenance. Cleanup is bounded and removes only old expired or terminal invitations, resolved claims whose parent link is terminal, and terminal links with no remaining claims. Pending join requests, organizations, memberships, active links, and message-journal recovery records are preserved.

Run `eng/verify.ps1` for boundaries, build, migration drift, unit tests, vulnerability audit, and PostgreSQL tests. Use `-SkipDocker` only when container tests are intentionally unavailable.
