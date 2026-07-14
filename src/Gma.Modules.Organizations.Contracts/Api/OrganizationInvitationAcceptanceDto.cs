namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationInvitationAcceptanceDto(
    OrganizationInvitationDto Invitation,
    OrganizationMembershipSummaryDto Membership);
