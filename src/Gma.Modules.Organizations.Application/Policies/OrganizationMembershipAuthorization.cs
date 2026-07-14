namespace Gma.Modules.Organizations.Application.Policies;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;

internal static class OrganizationMembershipAuthorization
{
    public static async Task<Result<OrganizationMembership>> RequireActiveAsync(
        IOrganizationRepository repository,
        Guid organizationId,
        string subjectId,
        CancellationToken cancellationToken)
    {
        OrganizationMembership? membership = await repository
            .GetMembershipAsync(organizationId, subjectId, cancellationToken)
            .ConfigureAwait(false);
        return membership is { Status: OrganizationMembershipState.Active }
            ? Result.Success(membership)
            : Result.Failure<OrganizationMembership>(OrganizationApplicationErrors.MembershipRequired);
    }

    public static async Task<Result<OrganizationMembership>> RequireOwnerAsync(
        IOrganizationRepository repository,
        Guid organizationId,
        string subjectId,
        CancellationToken cancellationToken)
    {
        Result<OrganizationMembership> membership = await RequireActiveAsync(
            repository, organizationId, subjectId, cancellationToken).ConfigureAwait(false);
        return membership.IsSuccess && membership.Value.Role == OrganizationMembershipRole.Owner
            ? membership
            : Result.Failure<OrganizationMembership>(OrganizationApplicationErrors.OwnerRequired);
    }
}
