namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationMembershipSummaryDto(
    OrganizationDto Organization,
    OrganizationMembershipDto Membership);
