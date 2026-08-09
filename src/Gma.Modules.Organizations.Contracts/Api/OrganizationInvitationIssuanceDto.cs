namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationInvitationIssuanceDto(
    OrganizationInvitationDto Invitation,
    string? Token,
    OrganizationJoinSourceIssuanceOutcome Outcome);
