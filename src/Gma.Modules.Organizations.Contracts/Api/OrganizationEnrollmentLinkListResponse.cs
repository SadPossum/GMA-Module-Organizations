namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationEnrollmentLinkListResponse(
    IReadOnlyList<OrganizationEnrollmentLinkDto> Items,
    int Page,
    int PageSize);
