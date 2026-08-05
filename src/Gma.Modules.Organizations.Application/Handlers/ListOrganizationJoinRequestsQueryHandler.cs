namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application.Policies;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Application.Queries;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;

internal sealed class ListOrganizationJoinRequestsQueryHandler(
    IOrganizationRepository organizations,
    OrganizationJoinSourceAuthorization joinSourceAuthorization,
    ISystemClock clock)
    : IQueryHandler<ListOrganizationJoinRequestsQuery, OrganizationJoinRequestListResponse>
{
    public async Task<Result<OrganizationJoinRequestListResponse>> HandleAsync(
        ListOrganizationJoinRequestsQuery query,
        CancellationToken cancellationToken)
    {
        Result authorized = await joinSourceAuthorization.AuthorizeAsync(
            new OrganizationJoinSourceAuthorizationContext(
                OrganizationJoinSourceAuthorizationOperation.ReadJoinRequests,
                query.OrganizationId,
                query.SubjectId),
            cancellationToken).ConfigureAwait(false);
        if (authorized.IsFailure)
        {
            return Result.Failure<OrganizationJoinRequestListResponse>(
                authorized.Error);
        }

        PageRequest page = PageRequest.Normalize(query.Page, query.PageSize);
        return Result.Success(await organizations.ListPendingJoinRequestsAsync(
            query.OrganizationId, page, clock.UtcNow, cancellationToken).ConfigureAwait(false));
    }
}
