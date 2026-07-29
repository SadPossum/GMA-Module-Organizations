namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Application.Queries;
using Gma.Modules.Organizations.Contracts;

internal sealed class ListOrganizationMembersForAdministrationQueryHandler(
    IOrganizationRepository organizations)
    : IQueryHandler<ListOrganizationMembersForAdministrationQuery, OrganizationMemberListResponse>
{
    public async Task<Result<OrganizationMemberListResponse>> HandleAsync(
        ListOrganizationMembersForAdministrationQuery query,
        CancellationToken cancellationToken)
    {
        if (await organizations.GetOrganizationAsync(query.OrganizationId, cancellationToken)
                .ConfigureAwait(false) is null)
        {
            return Result.Failure<OrganizationMemberListResponse>(
                OrganizationApplicationErrors.OrganizationNotFound);
        }

        PageRequest page = PageRequest.Normalize(query.Page, query.PageSize);
        return Result.Success(await organizations.ListMembersAsync(
            query.OrganizationId, page, cancellationToken).ConfigureAwait(false));
    }
}
