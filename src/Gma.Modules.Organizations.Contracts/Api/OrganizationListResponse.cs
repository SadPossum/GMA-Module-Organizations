namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationListResponse(
    IReadOnlyList<OrganizationMembershipSummaryDto> Items,
    int Page,
    int PageSize,
    bool HasMore = false);
