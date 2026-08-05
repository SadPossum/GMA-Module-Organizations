# GMA Organizations Module

Reusable organization, membership, invitation, and enrollment governance for GMA applications.

The Contracts package exposes an owner-protected, idempotent ordinary-membership lifecycle facade for product-owned onboarding and offboarding coordinators. Product employment and authorization policy remains outside Organizations.

Composed modules may reconcile one known enrollment claim through
`IOrganizationEnrollmentClaimInspector`. The exact-key, read-only contract
returns retained authoritative claim state without exposing persistence or
product cleanup policy.

Hosts may register `IOrganizationMutationAdmissionPolicy` implementations to
admit, deny, or temporarily defer owner-facing organization and join-source
mutations before Organizations changes state. The seam is optional and contains
no product lifecycle vocabulary.

This repository is consumed source-first and is mounted by composition repositories as `gma/modules/organizations`. Product names such as workspace, team, or account group remain outside this module.

Useful entry points:

- `Gma.Modules.Organizations.slnx`
- `docs/README.md`
- `docs/organizations-task.md`
- `docs/organizations-scope-lifecycle-task.md`
- `eng/verify.ps1`

Hosts may configure `OrganizationsApiSecurityOptions.GovernanceOperationsAssurance`
with a GMA `AuthenticationAssuranceRequirement`. When configured, organization
creation and governance mutations require that assurance; catalog reads,
invitation acceptance, and enrollment claims retain their existing behavior.
