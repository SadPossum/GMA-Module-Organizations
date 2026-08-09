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

internal sealed class EnsureOrganizationMembershipStateCommandHandler(
    IOrganizationRepository organizations,
    IOrganizationGovernanceCoordinator governance,
    OrganizationMutationAdmissionPolicy mutationAdmission,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<EnsureOrganizationMembershipStateCommand, OrganizationMembershipLifecycleResult>
{
    public async Task<Result<OrganizationMembershipLifecycleResult>> HandleAsync(
        EnsureOrganizationMembershipStateCommand command,
        CancellationToken cancellationToken)
    {
        await governance.AcquireExclusiveAsync(
            command.OrganizationId,
            cancellationToken).ConfigureAwait(false);

        OrganizationMembership? membership = await organizations
            .GetMembershipAsync(command.OrganizationId, command.SubjectId, cancellationToken)
            .ConfigureAwait(false);
        if (membership is null)
        {
            return Result.Success(new OrganizationMembershipLifecycleResult(
                OrganizationMembershipLifecycleOutcome.NotFound,
                null));
        }

        if (membership.Role == DomainMembershipRole.Owner)
        {
            return Result.Success(new OrganizationMembershipLifecycleResult(
                OrganizationMembershipLifecycleOutcome.OwnerProtected,
                membership.ToDto()));
        }

        OrganizationMembershipState? desiredState = ToDomain(command.DesiredStatus);
        if (desiredState is null)
        {
            return Result.Failure<OrganizationMembershipLifecycleResult>(
                OrganizationApplicationErrors.MembershipConflict);
        }

        if (membership.Status == desiredState)
        {
            return Result.Success(new OrganizationMembershipLifecycleResult(
                OrganizationMembershipLifecycleOutcome.AlreadyInDesiredState,
                membership.ToDto()));
        }

        if (desiredState == OrganizationMembershipState.Active)
        {
            Result admitted = await mutationAdmission.AuthorizeAsync(
                new OrganizationMutationAdmissionContext(
                    OrganizationMutationAdmissionOperation.RestoreMembership,
                    command.OrganizationId,
                    command.ActorId,
                    membership.Id,
                    membership.SubjectId),
                cancellationToken).ConfigureAwait(false);
            if (admitted.IsFailure)
            {
                return Result.Failure<OrganizationMembershipLifecycleResult>(
                    admitted.Error);
            }
        }

        Result changed = ChangeState(membership, desiredState.Value, command.ActorId, ids.NewId(), clock.UtcNow);
        if (changed.IsFailure)
        {
            return Result.Success(new OrganizationMembershipLifecycleResult(
                OrganizationMembershipLifecycleOutcome.TransitionNotAllowed,
                membership.ToDto()));
        }

        return Result.Success(new OrganizationMembershipLifecycleResult(
            OrganizationMembershipLifecycleOutcome.Changed,
            membership.ToDto()));
    }

    private static Result ChangeState(
        OrganizationMembership membership,
        OrganizationMembershipState desiredState,
        string actorId,
        Guid eventId,
        DateTimeOffset nowUtc) =>
        desiredState switch
        {
            OrganizationMembershipState.Active => membership.RestoreAsMember(
                membership.Version, actorId, eventId, nowUtc),
            OrganizationMembershipState.Suspended => membership.Suspend(
                membership.Version, actorId, eventId, nowUtc),
            OrganizationMembershipState.Removed => membership.Remove(
                membership.Version, actorId, eventId, nowUtc),
            _ => Result.Failure(OrganizationApplicationErrors.MembershipConflict)
        };

    private static OrganizationMembershipState? ToDomain(OrganizationMembershipStatus status) =>
        status switch
        {
            OrganizationMembershipStatus.Active => OrganizationMembershipState.Active,
            OrganizationMembershipStatus.Suspended => OrganizationMembershipState.Suspended,
            OrganizationMembershipStatus.Removed => OrganizationMembershipState.Removed,
            _ => null
        };
}
