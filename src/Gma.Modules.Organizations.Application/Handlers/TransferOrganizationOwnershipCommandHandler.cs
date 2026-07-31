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

        Result<OrganizationMembership> currentOwner = await OrganizationMembershipAuthorization.RequireOwnerAsync(
            organizations, command.OrganizationId, command.SubjectId, cancellationToken).ConfigureAwait(false);
        if (currentOwner.IsFailure)
        {
            return Result.Failure<OrganizationMembershipDto>(currentOwner.Error);
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

        Result demoted = currentOwner.Value.DemoteToMember(
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
}
