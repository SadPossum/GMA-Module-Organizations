namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationMembershipLifecycleResult(
    OrganizationMembershipLifecycleOutcome Outcome,
    OrganizationMembershipDto? Membership);
