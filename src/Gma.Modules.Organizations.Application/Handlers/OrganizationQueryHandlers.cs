namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Application.Policies;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Application.Queries;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;

internal sealed class ListMyOrganizationsQueryHandler(IOrganizationRepository organizations)
    : IQueryHandler<ListMyOrganizationsQuery, OrganizationListResponse>
{
    public async Task<Result<OrganizationListResponse>> HandleAsync(
        ListMyOrganizationsQuery query,
        CancellationToken cancellationToken)
    {
        PageRequest page = PageRequest.Normalize(query.Page, query.PageSize);
        return Result.Success(await organizations.ListForSubjectAsync(
            query.SubjectId, page.Page, page.PageSize, cancellationToken).ConfigureAwait(false));
    }
}

internal sealed class GetOrganizationQueryHandler(IOrganizationRepository organizations)
    : IQueryHandler<GetOrganizationQuery, OrganizationMembershipSummaryDto>
{
    public async Task<Result<OrganizationMembershipSummaryDto>> HandleAsync(
        GetOrganizationQuery query,
        CancellationToken cancellationToken)
    {
        Result<OrganizationMembership> membership = await OrganizationMembershipAuthorization.RequireActiveAsync(
            organizations, query.OrganizationId, query.SubjectId, cancellationToken).ConfigureAwait(false);
        if (membership.IsFailure)
        {
            return Result.Failure<OrganizationMembershipSummaryDto>(membership.Error);
        }

        Organization? organization = await organizations
            .GetOrganizationAsync(query.OrganizationId, cancellationToken)
            .ConfigureAwait(false);
        return organization is null
            ? Result.Failure<OrganizationMembershipSummaryDto>(OrganizationApplicationErrors.OrganizationNotFound)
            : Result.Success(new OrganizationMembershipSummaryDto(
                organization.ToDto(), membership.Value.ToDto()));
    }
}

internal sealed class ListOrganizationMembersQueryHandler(IOrganizationRepository organizations)
    : IQueryHandler<ListOrganizationMembersQuery, OrganizationMemberListResponse>
{
    public async Task<Result<OrganizationMemberListResponse>> HandleAsync(
        ListOrganizationMembersQuery query,
        CancellationToken cancellationToken)
    {
        Result<OrganizationMembership> owner = await OrganizationMembershipAuthorization.RequireOwnerAsync(
            organizations, query.OrganizationId, query.SubjectId, cancellationToken).ConfigureAwait(false);
        if (owner.IsFailure)
        {
            return Result.Failure<OrganizationMemberListResponse>(owner.Error);
        }

        PageRequest page = PageRequest.Normalize(query.Page, query.PageSize);
        return Result.Success(await organizations.ListMembersAsync(
            query.OrganizationId, page.Page, page.PageSize, cancellationToken).ConfigureAwait(false));
    }
}
