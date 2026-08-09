namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationJoinRequestListResponse(
    IReadOnlyList<OrganizationEnrollmentClaimDto> Items,
    int Page,
    int PageSize,
    bool HasMore = false);
