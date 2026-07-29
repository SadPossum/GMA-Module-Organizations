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

internal sealed class ListOrganizationEnrollmentLinksQueryHandler(
    IOrganizationRepository organizations,
    ISystemClock clock) : IQueryHandler<ListOrganizationEnrollmentLinksQuery, OrganizationEnrollmentLinkListResponse>
{
    public async Task<Result<OrganizationEnrollmentLinkListResponse>> HandleAsync(
        ListOrganizationEnrollmentLinksQuery query,
        CancellationToken cancellationToken)
    {
        Result<OrganizationMembership> owner = await OrganizationMembershipAuthorization.RequireOwnerAsync(
            organizations, query.OrganizationId, query.SubjectId, cancellationToken).ConfigureAwait(false);
        if (owner.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentLinkListResponse>(owner.Error);
        }

        PageRequest page = PageRequest.Normalize(query.Page, query.PageSize);
        return Result.Success(await organizations.ListEnrollmentLinksAsync(
            query.OrganizationId, page, clock.UtcNow, cancellationToken).ConfigureAwait(false));
    }
}
