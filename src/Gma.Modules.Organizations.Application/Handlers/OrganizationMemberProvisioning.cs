namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using DomainMembershipRole = Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;

internal static class OrganizationMemberProvisioning
{
    public static async Task<Result<OrganizationMembership>> EnsureActiveMemberAsync(
        IOrganizationRepository organizations,
        OrganizationMembership? membership,
        Guid organizationId,
        string subjectId,
        string actorId,
        DateTimeOffset nowUtc,
        IIdGenerator ids,
        CancellationToken cancellationToken)
    {
        if (membership is { Status: OrganizationMembershipState.Active })
        {
            return Result.Success(membership);
        }

        if (membership is not null)
        {
            Result restored = membership.RestoreAsMember(
                membership.Version, actorId, ids.NewId(), nowUtc);
            return restored.IsSuccess
                ? Result.Success(membership)
                : Result.Failure<OrganizationMembership>(restored.Error);
        }

        Result<OrganizationMembership> created = OrganizationMembership.Create(
            ids.NewId(), organizationId, subjectId, DomainMembershipRole.Member,
            actorId, ids.NewId(), nowUtc);
        if (created.IsFailure)
        {
            return created;
        }

        await organizations.AddMembershipAsync(created.Value, cancellationToken).ConfigureAwait(false);
        return created;
    }
}
