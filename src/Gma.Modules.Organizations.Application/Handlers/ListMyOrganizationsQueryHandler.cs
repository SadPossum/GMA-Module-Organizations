namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Application.Queries;
using Gma.Modules.Organizations.Contracts;

internal sealed class ListMyOrganizationsQueryHandler(IOrganizationRepository organizations)
    : IQueryHandler<ListMyOrganizationsQuery, OrganizationListResponse>
{
    public async Task<Result<OrganizationListResponse>> HandleAsync(
        ListMyOrganizationsQuery query,
        CancellationToken cancellationToken)
    {
        PageRequest page = PageRequest.Normalize(query.Page, query.PageSize);
        return Result.Success(await organizations.ListForSubjectAsync(
            query.SubjectId, page, cancellationToken).ConfigureAwait(false));
    }
}
