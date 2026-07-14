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

internal sealed class ChangeOrganizationMembershipCommandHandler(
    IOrganizationRepository organizations,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<ChangeOrganizationMembershipCommand, OrganizationMembershipDto>
{
    public async Task<Result<OrganizationMembershipDto>> HandleAsync(
        ChangeOrganizationMembershipCommand command,
        CancellationToken cancellationToken)
    {
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
}
