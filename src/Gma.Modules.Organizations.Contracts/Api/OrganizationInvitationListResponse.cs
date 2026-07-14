namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationInvitationListResponse(
    IReadOnlyList<OrganizationInvitationDto> Items,
    int Page,
    int PageSize);
