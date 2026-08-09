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
        await governance.AcquireExclusiveAsync(
            command.OrganizationId,
            cancellationToken).ConfigureAwait(false);

        Result<OrganizationMembership> owner = await OrganizationMembershipAuthorization.RequireOwnerAsync(
            organizations, command.OrganizationId, command.SubjectId, cancellationToken).ConfigureAwait(false);
        if (owner.IsFailure)
        {
            return Result.Failure<OrganizationMembershipDto>(owner.Error);
        }

        Organization? organization = await organizations
            .GetOrganizationAsync(command.OrganizationId, cancellationToken)
            .ConfigureAwait(false);
        OrganizationMembership? membership = await organizations
            .GetMembershipAsync(command.OrganizationId, command.TargetSubjectId, cancellationToken)
            .ConfigureAwait(false);
        if (organization is null)
        {
            return Result.Failure<OrganizationMembershipDto>(OrganizationApplicationErrors.OrganizationNotFound);
        }

        if (membership is null)
        {
            return Result.Failure<OrganizationMembershipDto>(OrganizationApplicationErrors.MembershipNotFound);
        }

        ContractMembershipStatus requestedStatus = command.Action switch
        {
            OrganizationMembershipAction.Suspend => ContractMembershipStatus.Suspended,
            OrganizationMembershipAction.Resume => ContractMembershipStatus.Active,
            OrganizationMembershipAction.Remove => ContractMembershipStatus.Removed,
            _ => ContractMembershipStatus.Unknown
        };
        if (requestedStatus == ContractMembershipStatus.Unknown)
        {
            return Result.Failure<OrganizationMembershipDto>(OrganizationApplicationErrors.MembershipNotFound);
        }

        OrganizationMembershipChangePolicyRequest policyRequest = new(
            command.OrganizationId,
            command.SubjectId,
            command.TargetSubjectId,
            ToContractRole(membership.Role),
            ToContractStatus(membership.Status),
            requestedStatus);
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
            organization, membership, command, nowUtc);
        if (ownerCount.IsFailure)
        {
            return Result.Failure<OrganizationMembershipDto>(ownerCount.Error);
        }

        Result changed = command.Action switch
        {
            OrganizationMembershipAction.Suspend => membership.Suspend(
                command.ExpectedMembershipVersion, command.ActorId, ids.NewId(), nowUtc),
            OrganizationMembershipAction.Resume => membership.Resume(
                command.ExpectedMembershipVersion, command.ActorId, ids.NewId(), nowUtc),
            OrganizationMembershipAction.Remove => membership.Remove(
                command.ExpectedMembershipVersion, command.ActorId, ids.NewId(), nowUtc),
            _ => Result.Failure(OrganizationApplicationErrors.MembershipNotFound)
        };

        return changed.IsSuccess
            ? Result.Success(membership.ToDto())
            : Result.Failure<OrganizationMembershipDto>(changed.Error);
    }

    private Result ChangeOwnerCountIfNeeded(
        Organization organization,
        OrganizationMembership membership,
        ChangeOrganizationMembershipCommand command,
        DateTimeOffset nowUtc)
    {
        if (membership.Role != DomainMembershipRole.Owner)
        {
            return Result.Success();
        }

        bool removesActiveOwner = membership.Status == OrganizationMembershipState.Active &&
            command.Action is OrganizationMembershipAction.Suspend or OrganizationMembershipAction.Remove;
        bool restoresOwner = membership.Status == OrganizationMembershipState.Suspended &&
            command.Action == OrganizationMembershipAction.Resume;

        if (removesActiveOwner)
        {
            return organization.RemoveActiveOwner(
                command.ExpectedOrganizationVersion, command.ActorId, ids.NewId(), nowUtc);
        }

        return restoresOwner
            ? organization.AddActiveOwner(
                command.ExpectedOrganizationVersion, command.ActorId, ids.NewId(), nowUtc)
            : Result.Success();
    }

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
