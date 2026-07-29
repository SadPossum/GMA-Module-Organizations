namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Application.Policies;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Application.Queries;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;

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
