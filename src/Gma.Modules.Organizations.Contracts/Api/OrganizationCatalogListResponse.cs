namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationCatalogListResponse(
    IReadOnlyList<OrganizationDto> Items,
    int Page,
    int PageSize,
    bool HasMore = false);
