namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Application.Queries;
using Gma.Modules.Organizations.Contracts;

internal sealed class ListOrganizationCatalogForAdministrationQueryHandler(
    IOrganizationRepository organizations)
    : IQueryHandler<ListOrganizationCatalogForAdministrationQuery, OrganizationCatalogListResponse>
{
    public async Task<Result<OrganizationCatalogListResponse>> HandleAsync(
        ListOrganizationCatalogForAdministrationQuery query,
        CancellationToken cancellationToken)
    {
        PageRequest page = PageRequest.Normalize(query.Page, query.PageSize);
        return Result.Success(await organizations.ListCatalogAsync(
            page, cancellationToken).ConfigureAwait(false));
    }
}
