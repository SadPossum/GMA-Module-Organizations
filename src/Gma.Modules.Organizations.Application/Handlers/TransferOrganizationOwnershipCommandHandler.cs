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
using DomainMembershipRole = Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;

internal sealed class TransferOrganizationOwnershipCommandHandler(
    IOrganizationRepository organizations,
    IOrganizationGovernanceCoordinator governance,
    OrganizationMutationAdmissionPolicy mutationAdmission,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<TransferOrganizationOwnershipCommand, OrganizationMembershipDto>
{
    public async Task<Result<OrganizationMembershipDto>> HandleAsync(
        TransferOrganizationOwnershipCommand command,
        CancellationToken cancellationToken)
    {
        if (string.Equals(command.SubjectId.Trim(), command.TargetSubjectId.Trim(), StringComparison.Ordinal))
        {
            return Result.Failure<OrganizationMembershipDto>(
                OrganizationApplicationErrors.OwnershipTargetMustDiffer);
        }

        await governance.AcquireExclusiveAsync(
            command.OrganizationId,
            cancellationToken).ConfigureAwait(false);

        OrganizationMembership? currentOwner = await organizations
            .GetMembershipAsync(command.OrganizationId, command.SubjectId, cancellationToken)
            .ConfigureAwait(false);
        if (currentOwner is not
            {
                Status: OrganizationMembershipState.Active,
                Role: DomainMembershipRole.Owner
            })
        {
            return await this.TryReplayAsync(
                command,
                currentOwner,
                cancellationToken).ConfigureAwait(false);
        }

        Organization? organization = await organizations
            .GetOrganizationAsync(command.OrganizationId, cancellationToken)
            .ConfigureAwait(false);
        OrganizationMembership? target = await organizations
            .GetMembershipAsync(command.OrganizationId, command.TargetSubjectId, cancellationToken)
            .ConfigureAwait(false);
        if (organization is null)
        {
            return Result.Failure<OrganizationMembershipDto>(OrganizationApplicationErrors.OrganizationNotFound);
        }

        if (target is not { Status: OrganizationMembershipState.Active })
        {
            return Result.Failure<OrganizationMembershipDto>(OrganizationApplicationErrors.MembershipRequired);
        }

        Result admitted = await mutationAdmission.AuthorizeAsync(
            new OrganizationMutationAdmissionContext(
                OrganizationMutationAdmissionOperation.TransferOwnership,
                command.OrganizationId,
                command.SubjectId,
                TargetSubjectId: command.TargetSubjectId),
            cancellationToken).ConfigureAwait(false);
        if (admitted.IsFailure)
        {
            return Result.Failure<OrganizationMembershipDto>(admitted.Error);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        bool targetWasOwner = target.Role == DomainMembershipRole.Owner;
        if (!targetWasOwner)
        {
            Result promoted = target.PromoteToOwner(
                command.ExpectedTargetVersion, command.ActorId, ids.NewId(), nowUtc);
            if (promoted.IsFailure)
            {
                return Result.Failure<OrganizationMembershipDto>(promoted.Error);
            }
        }

        Result demoted = currentOwner.DemoteToMember(
            command.ExpectedCurrentOwnerVersion, command.ActorId, ids.NewId(), nowUtc);
        if (demoted.IsFailure)
        {
            return Result.Failure<OrganizationMembershipDto>(demoted.Error);
        }

        Result ownerCount = targetWasOwner
            ? organization.RemoveActiveOwner(
                command.ExpectedOrganizationVersion, command.ActorId, ids.NewId(), nowUtc)
            : organization.RecordOwnerTransfer(
                command.ExpectedOrganizationVersion, command.ActorId, ids.NewId(), nowUtc);
        return ownerCount.IsSuccess
            ? Result.Success(target.ToDto())
            : Result.Failure<OrganizationMembershipDto>(ownerCount.Error);
    }

    private async Task<Result<OrganizationMembershipDto>> TryReplayAsync(
        TransferOrganizationOwnershipCommand command,
        OrganizationMembership? formerOwner,
        CancellationToken cancellationToken)
    {
        string? actorId = command.ActorId?.Trim();
        if (formerOwner is not
            {
                Status: OrganizationMembershipState.Active,
                Role: DomainMembershipRole.Member
            } ||
            !IsImmediatelyAfter(formerOwner.Version, command.ExpectedCurrentOwnerVersion) ||
            !string.Equals(formerOwner.LastChangedBy, actorId, StringComparison.Ordinal))
        {
            return Result.Failure<OrganizationMembershipDto>(
                OrganizationApplicationErrors.OwnerRequired);
        }

        Organization? organization = await organizations
            .GetOrganizationAsync(command.OrganizationId, cancellationToken)
            .ConfigureAwait(false);
        OrganizationMembership? target = await organizations
            .GetMembershipAsync(command.OrganizationId, command.TargetSubjectId, cancellationToken)
            .ConfigureAwait(false);
        if (!IsExactReplay(command, organization, formerOwner, target, actorId))
        {
            return Result.Failure<OrganizationMembershipDto>(
                OrganizationApplicationErrors.OwnerRequired);
        }

        return Result.Success(target!.ToDto());
    }

    private static bool IsExactReplay(
        TransferOrganizationOwnershipCommand command,
        Organization? organization,
        OrganizationMembership formerOwner,
        OrganizationMembership? target,
        string? actorId)
    {
        if (organization is not { Status: OrganizationState.Active } ||
            target is not
            {
                Status: OrganizationMembershipState.Active,
                Role: DomainMembershipRole.Owner
            } ||
            !IsImmediatelyAfter(organization.Version, command.ExpectedOrganizationVersion) ||
            !string.Equals(organization.LastChangedBy, actorId, StringComparison.Ordinal) ||
            organization.LastChangedAtUtc != formerOwner.LastChangedAtUtc)
        {
            return false;
        }

        bool targetWasPromoted =
            IsImmediatelyAfter(target.Version, command.ExpectedTargetVersion) &&
            string.Equals(target.LastChangedBy, actorId, StringComparison.Ordinal) &&
            target.LastChangedAtUtc == formerOwner.LastChangedAtUtc;
        bool targetWasAlreadyOwner = target.Version == command.ExpectedTargetVersion;
        return targetWasPromoted || targetWasAlreadyOwner;
    }

    private static bool IsImmediatelyAfter(long currentVersion, long expectedVersion) =>
        currentVersion > 1 && expectedVersion == currentVersion - 1;
}
