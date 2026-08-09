namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using DomainMembershipRole = Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;

internal sealed class EnsureOrganizationOwnerForAdministrationCommandHandler(
    IOrganizationRepository organizations,
    IOrganizationGovernanceCoordinator governance,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<EnsureOrganizationOwnerForAdministrationCommand, OrganizationMembershipSummaryDto>
{
    public async Task<Result<OrganizationMembershipSummaryDto>> HandleAsync(
        EnsureOrganizationOwnerForAdministrationCommand command,
        CancellationToken cancellationToken)
    {
        await governance.AcquireExclusiveAsync(
            command.OrganizationId,
            cancellationToken).ConfigureAwait(false);

        Organization? organization = await organizations.GetOrganizationAsync(
            command.OrganizationId, cancellationToken).ConfigureAwait(false);
        if (organization is null)
        {
            return Result.Failure<OrganizationMembershipSummaryDto>(
                OrganizationApplicationErrors.OrganizationNotFound);
        }

        OrganizationMembership? membership = await organizations.GetMembershipAsync(
            organization.Id, command.TargetSubjectId, cancellationToken).ConfigureAwait(false);
        if (membership is { Status: OrganizationMembershipState.Active, Role: DomainMembershipRole.Owner })
        {
            return Result.Success(new OrganizationMembershipSummaryDto(
                organization.ToDto(), membership.ToDto()));
        }

        if (organization.Version != command.ExpectedOrganizationVersion)
        {
            return Result.Failure<OrganizationMembershipSummaryDto>(
                Gma.Modules.Organizations.Domain.Errors.OrganizationDomainErrors.VersionConflict);
        }

        if (organization.Status != OrganizationState.Active)
        {
            return Result.Failure<OrganizationMembershipSummaryDto>(
                Gma.Modules.Organizations.Domain.Errors.OrganizationDomainErrors.OrganizationNotActive);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result<OrganizationMembership> owner = membership is null
            ? await this.CreateOwnerAsync(organization, command, nowUtc, cancellationToken).ConfigureAwait(false)
            : this.RestoreOrPromoteOwner(membership, command, nowUtc);
        if (owner.IsFailure)
        {
            return Result.Failure<OrganizationMembershipSummaryDto>(owner.Error);
        }

        Result ownerAdded = organization.AddActiveOwner(
            command.ExpectedOrganizationVersion, command.ActorId, ids.NewId(), nowUtc);
        return ownerAdded.IsSuccess
            ? Result.Success(new OrganizationMembershipSummaryDto(
                organization.ToDto(), owner.Value.ToDto()))
            : Result.Failure<OrganizationMembershipSummaryDto>(ownerAdded.Error);
    }

    private async Task<Result<OrganizationMembership>> CreateOwnerAsync(
        Organization organization,
        EnsureOrganizationOwnerForAdministrationCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (command.ExpectedMembershipVersion is not null)
        {
            return Result.Failure<OrganizationMembership>(OrganizationApplicationErrors.MembershipNotFound);
        }

        Result<OrganizationMembership> created = OrganizationMembership.Create(
            ids.NewId(), organization.Id, command.TargetSubjectId, DomainMembershipRole.Owner,
            command.ActorId, ids.NewId(), nowUtc);
        if (created.IsSuccess)
        {
            await organizations.AddMembershipAsync(created.Value, cancellationToken).ConfigureAwait(false);
        }

        return created;
    }

    private Result<OrganizationMembership> RestoreOrPromoteOwner(
        OrganizationMembership membership,
        EnsureOrganizationOwnerForAdministrationCommand command,
        DateTimeOffset nowUtc)
    {
        if (command.ExpectedMembershipVersion != membership.Version)
        {
            return Result.Failure<OrganizationMembership>(
                Gma.Modules.Organizations.Domain.Errors.OrganizationDomainErrors.VersionConflict);
        }

        if (membership.Status != OrganizationMembershipState.Active)
        {
            Result restored = membership.RestoreAsMember(
                membership.Version, command.ActorId, ids.NewId(), nowUtc);
            if (restored.IsFailure)
            {
                return Result.Failure<OrganizationMembership>(restored.Error);
            }
        }

        Result promoted = membership.PromoteToOwner(
            membership.Version, command.ActorId, ids.NewId(), nowUtc);
        return promoted.IsSuccess
            ? Result.Success(membership)
            : Result.Failure<OrganizationMembership>(promoted.Error);
    }
}
