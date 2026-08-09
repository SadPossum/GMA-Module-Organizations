namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using DomainMembershipRole = Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;

internal readonly record struct OrganizationMembershipMutation(
    OrganizationMembershipAction Action,
    OrganizationMembershipStatus RequestedStatus,
    OrganizationMembershipChangeKind ChangeKind,
    OrganizationMembershipState ResultingState)
{
    public static bool TryResolve(
        OrganizationMembershipAction action,
        out OrganizationMembershipMutation mutation)
    {
        mutation = action switch
        {
            OrganizationMembershipAction.Suspend => new(
                action,
                OrganizationMembershipStatus.Suspended,
                OrganizationMembershipChangeKind.Suspended,
                OrganizationMembershipState.Suspended),
            OrganizationMembershipAction.Resume => new(
                action,
                OrganizationMembershipStatus.Active,
                OrganizationMembershipChangeKind.Resumed,
                OrganizationMembershipState.Active),
            OrganizationMembershipAction.Remove => new(
                action,
                OrganizationMembershipStatus.Removed,
                OrganizationMembershipChangeKind.Removed,
                OrganizationMembershipState.Removed),
            _ => default
        };
        return mutation.Action != OrganizationMembershipAction.Unknown;
    }

    public bool ChangesOwnerCount(OrganizationMembership membership) =>
        membership.Role == DomainMembershipRole.Owner &&
        (membership.Status == OrganizationMembershipState.Active &&
            this.Action is OrganizationMembershipAction.Suspend or OrganizationMembershipAction.Remove ||
         membership.Status == OrganizationMembershipState.Suspended &&
            this.Action == OrganizationMembershipAction.Resume);

    public bool IsExactReplay(
        Organization organization,
        OrganizationMembership membership,
        Guid operationId,
        long expectedOrganizationVersion,
        long expectedMembershipVersion,
        string actorId)
    {
        if (!membership.IsExactMutationReplay(
                operationId,
                this.ChangeKind,
                this.ResultingState,
                expectedMembershipVersion,
                actorId))
        {
            return false;
        }

        if (membership.Role != DomainMembershipRole.Owner)
        {
            return true;
        }

        bool ownerCountWasUnchanged =
            this.Action == OrganizationMembershipAction.Remove &&
            organization.Version == expectedOrganizationVersion;
        return ownerCountWasUnchanged || organization.IsExactOwnerCountMutationReplay(
            operationId,
            expectedOrganizationVersion,
            actorId,
            membership.LastChangedAtUtc);
    }

    public Result Apply(
        OrganizationMembership membership,
        long expectedVersion,
        string actorId,
        Guid operationId,
        Guid eventId,
        DateTimeOffset nowUtc) =>
        this.Action switch
        {
            OrganizationMembershipAction.Suspend => membership.Suspend(
                expectedVersion,
                actorId,
                eventId,
                nowUtc,
                operationId),
            OrganizationMembershipAction.Resume => membership.Resume(
                expectedVersion,
                actorId,
                eventId,
                nowUtc,
                operationId),
            OrganizationMembershipAction.Remove => membership.Remove(
                expectedVersion,
                actorId,
                eventId,
                nowUtc,
                operationId),
            _ => Result.Failure(OrganizationApplicationErrors.MembershipNotFound)
        };
}
