namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationMemberListResponse(
    IReadOnlyList<OrganizationMembershipDto> Items,
    int Page,
    int PageSize);
