namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Application.Policies;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Domain.ValueObjects;
using ContractMembershipRole = Gma.Modules.Organizations.Contracts.OrganizationMembershipRole;
using ContractMembershipStatus = Gma.Modules.Organizations.Contracts.OrganizationMembershipStatus;
using DomainMembershipRole = Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;

internal sealed class ChangeOrganizationMembershipCommandHandler(
    IOrganizationRepository organizations,
    IOrganizationGovernanceCoordinator governance,
    IEnumerable<IOrganizationMembershipChangePolicy> membershipChangePolicies,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<ChangeOrganizationMembershipCommand, OrganizationMembershipDto>
{
    public async Task<Result<OrganizationMembershipDto>> HandleAsync(
        ChangeOrganizationMembershipCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<OrganizationMembershipDto>(
                OrganizationApplicationErrors.MutationOperationRequired);
        }

        if (!OrganizationMembershipMutation.TryResolve(
                command.Action,
                out OrganizationMembershipMutation mutation))
        {
            return Result.Failure<OrganizationMembershipDto>(
                OrganizationApplicationErrors.MembershipNotFound);
        }

        Result<OrganizationActorId> actor = OrganizationActorId.Create(
            command.ActorId);
        if (actor.IsFailure)
        {
            return Result.Failure<OrganizationMembershipDto>(actor.Error);
        }

        await governance.AcquireExclusiveAsync(
            command.OrganizationId,
            cancellationToken).ConfigureAwait(false);

        Result<OrganizationMembership> owner = await OrganizationMembershipAuthorization.RequireOwnerAsync(
            organizations, command.OrganizationId, command.SubjectId, cancellationToken).ConfigureAwait(false);
        Organization? organization = await organizations
            .GetOrganizationAsync(command.OrganizationId, cancellationToken)
            .ConfigureAwait(false);
        OrganizationMembership? membership = await organizations
            .GetMembershipAsync(command.OrganizationId, command.TargetSubjectId, cancellationToken)
            .ConfigureAwait(false);
        if (owner.IsFailure)
        {
            if (organization is null || membership is null ||
                !IsSelfReplayCandidate(command, membership, actor.Value.Value))
            {
                return Result.Failure<OrganizationMembershipDto>(owner.Error);
            }

            return mutation.IsExactReplay(
                organization,
                membership,
                command.OperationId,
                command.ExpectedOrganizationVersion,
                command.ExpectedMembershipVersion,
                actor.Value.Value)
                ? Result.Success(membership.ToDto())
                : Result.Failure<OrganizationMembershipDto>(
                    OrganizationApplicationErrors.MutationOperationConflict);
        }

        if (organization is null)
        {
            return Result.Failure<OrganizationMembershipDto>(
                OrganizationApplicationErrors.OrganizationNotFound);
        }

        if (membership is null)
        {
            return Result.Failure<OrganizationMembershipDto>(
                OrganizationApplicationErrors.MembershipNotFound);
        }

        if (membership.HasLastMutationOperation(command.OperationId))
        {
            return mutation.IsExactReplay(
                organization,
                membership,
                command.OperationId,
                command.ExpectedOrganizationVersion,
                command.ExpectedMembershipVersion,
                actor.Value.Value)
                ? Result.Success(membership.ToDto())
                : Result.Failure<OrganizationMembershipDto>(
                    OrganizationApplicationErrors.MutationOperationConflict);
        }

        if (membership.Version != command.ExpectedMembershipVersion ||
            mutation.ChangesOwnerCount(membership) &&
            organization.Version != command.ExpectedOrganizationVersion)
        {
            return Result.Failure<OrganizationMembershipDto>(
                OrganizationApplicationErrors.VersionConflict);
        }

        OrganizationMembershipChangePolicyRequest policyRequest = new(
            command.OrganizationId,
            command.SubjectId,
            command.TargetSubjectId,
            ToContractRole(membership.Role),
            ToContractStatus(membership.Status),
            mutation.RequestedStatus);
        foreach (IOrganizationMembershipChangePolicy policy in membershipChangePolicies)
        {
            OrganizationMembershipChangePolicyDecision decision = await policy
                .EvaluateAsync(policyRequest, cancellationToken)
                .ConfigureAwait(false);
            if (decision != OrganizationMembershipChangePolicyDecision.Allowed)
            {
                return Result.Failure<OrganizationMembershipDto>(
                    OrganizationApplicationErrors.MembershipChangeRejected);
            }
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result ownerCount = ChangeOwnerCountIfNeeded(
            organization,
            membership,
            command,
            mutation,
            actor.Value.Value,
            nowUtc);
        if (ownerCount.IsFailure)
        {
            return Result.Failure<OrganizationMembershipDto>(ownerCount.Error);
        }

        Result changed = mutation.Apply(
            membership,
            command.ExpectedMembershipVersion,
            actor.Value.Value,
            command.OperationId,
            ids.NewId(),
            nowUtc);

        return changed.IsSuccess
            ? Result.Success(membership.ToDto())
            : Result.Failure<OrganizationMembershipDto>(changed.Error);
    }

    private Result ChangeOwnerCountIfNeeded(
        Organization organization,
        OrganizationMembership membership,
        ChangeOrganizationMembershipCommand command,
        OrganizationMembershipMutation mutation,
        string actorId,
        DateTimeOffset nowUtc)
    {
        if (membership.Role != DomainMembershipRole.Owner)
        {
            return Result.Success();
        }

        bool removesActiveOwner = membership.Status == OrganizationMembershipState.Active &&
            mutation.Action is OrganizationMembershipAction.Suspend or OrganizationMembershipAction.Remove;
        bool restoresOwner = membership.Status == OrganizationMembershipState.Suspended &&
            mutation.Action == OrganizationMembershipAction.Resume;

        if (removesActiveOwner)
        {
            return organization.RemoveActiveOwner(
                command.ExpectedOrganizationVersion,
                actorId,
                ids.NewId(),
                nowUtc,
                command.OperationId);
        }

        return restoresOwner
            ? organization.AddActiveOwner(
                command.ExpectedOrganizationVersion,
                actorId,
                ids.NewId(),
                nowUtc,
                command.OperationId)
            : Result.Success();
    }

    private static bool IsSelfReplayCandidate(
        ChangeOrganizationMembershipCommand command,
        OrganizationMembership membership,
        string actorId) =>
        membership.Role == DomainMembershipRole.Owner &&
        membership.HasLastMutationOperation(command.OperationId) &&
        string.Equals(
            membership.SubjectId,
            command.SubjectId.Trim(),
            StringComparison.Ordinal) &&
        string.Equals(
            membership.LastChangedBy,
            actorId,
            StringComparison.Ordinal);

    private static ContractMembershipRole ToContractRole(DomainMembershipRole role) => role switch
    {
        DomainMembershipRole.Owner => ContractMembershipRole.Owner,
        DomainMembershipRole.Member => ContractMembershipRole.Member,
        _ => ContractMembershipRole.Unknown
    };

    private static ContractMembershipStatus ToContractStatus(OrganizationMembershipState status) => status switch
    {
        OrganizationMembershipState.Active => ContractMembershipStatus.Active,
        OrganizationMembershipState.Suspended => ContractMembershipStatus.Suspended,
        OrganizationMembershipState.Removed => ContractMembershipStatus.Removed,
        _ => ContractMembershipStatus.Unknown
    };
}
