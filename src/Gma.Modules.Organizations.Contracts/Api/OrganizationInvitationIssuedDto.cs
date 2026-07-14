namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationInvitationIssuedDto(
    OrganizationInvitationDto Invitation,
    string Token);
