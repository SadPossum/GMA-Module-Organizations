# GMA Organizations Module

Reusable organization, membership, invitation, and enrollment governance for GMA applications.

The Contracts package exposes an owner-protected, idempotent ordinary-membership lifecycle facade for product-owned onboarding and offboarding coordinators. Product employment and authorization policy remains outside Organizations.

This repository is consumed source-first and is mounted by composition repositories as `gma/modules/organizations`. Product names such as workspace, team, or account group remain outside this module.

Useful entry points:

- `Gma.Modules.Organizations.slnx`
- `docs/README.md`
- `docs/organizations-task.md`
- `eng/verify.ps1`

Hosts may configure `OrganizationsApiSecurityOptions.GovernanceOperationsAssurance`
with a GMA `AuthenticationAssuranceRequirement`. When configured, organization
creation and governance mutations require that assurance; catalog reads,
invitation acceptance, and enrollment claims retain their existing behavior.
