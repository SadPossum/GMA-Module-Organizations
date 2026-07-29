namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Application.Policies;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Application.Queries;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;

internal sealed class GetOrganizationEnrollmentLinkQueryHandler(
    IOrganizationRepository organizations,
    ISystemClock clock) : IQueryHandler<GetOrganizationEnrollmentLinkQuery, OrganizationEnrollmentLinkDto>
{
    public async Task<Result<OrganizationEnrollmentLinkDto>> HandleAsync(
        GetOrganizationEnrollmentLinkQuery query,
        CancellationToken cancellationToken)
    {
        Result<OrganizationMembership> owner = await OrganizationMembershipAuthorization.RequireOwnerAsync(
            organizations, query.OrganizationId, query.SubjectId, cancellationToken).ConfigureAwait(false);
        if (owner.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentLinkDto>(owner.Error);
        }

        OrganizationEnrollmentLink? link = await organizations.GetEnrollmentLinkAsync(
            query.OrganizationId, query.EnrollmentLinkId, cancellationToken).ConfigureAwait(false);
        return link is null
            ? Result.Failure<OrganizationEnrollmentLinkDto>(OrganizationApplicationErrors.EnrollmentLinkNotFound)
            : Result.Success(link.ToDto(clock.UtcNow));
    }
}
