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

internal sealed class ListOrganizationInvitationsQueryHandler(
    IOrganizationRepository organizations,
    OrganizationJoinSourceAuthorization joinSourceAuthorization,
    ISystemClock clock) : IQueryHandler<ListOrganizationInvitationsQuery, OrganizationInvitationListResponse>
{
    public async Task<Result<OrganizationInvitationListResponse>> HandleAsync(
        ListOrganizationInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        Result authorized = await joinSourceAuthorization.AuthorizeAsync(
            new OrganizationJoinSourceAuthorizationContext(
                OrganizationJoinSourceAuthorizationOperation.ReadInvitations,
                query.OrganizationId,
                query.SubjectId),
            cancellationToken).ConfigureAwait(false);
        if (authorized.IsFailure)
        {
            return Result.Failure<OrganizationInvitationListResponse>(authorized.Error);
        }

        PageRequest page = PageRequest.Normalize(query.Page, query.PageSize);
        return Result.Success(await organizations.ListInvitationsAsync(
            query.OrganizationId, page, clock.UtcNow, cancellationToken).ConfigureAwait(false));
    }
}
