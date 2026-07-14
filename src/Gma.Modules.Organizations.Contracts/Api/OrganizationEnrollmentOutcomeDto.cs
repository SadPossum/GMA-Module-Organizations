namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationEnrollmentOutcomeDto(
    OrganizationEnrollmentClaimDto Claim,
    OrganizationMembershipSummaryDto? Membership);
