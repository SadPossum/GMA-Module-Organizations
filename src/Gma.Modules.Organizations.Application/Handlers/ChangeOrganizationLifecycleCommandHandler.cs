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
using Gma.Modules.Organizations.Domain.ValueObjects;

internal sealed class ChangeOrganizationLifecycleCommandHandler(
    IOrganizationRepository organizations,
    IOrganizationGovernanceCoordinator governance,
    OrganizationMutationAdmissionPolicy mutationAdmission,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<ChangeOrganizationLifecycleCommand, OrganizationDto>
{
    public async Task<Result<OrganizationDto>> HandleAsync(
        ChangeOrganizationLifecycleCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<OrganizationDto>(
                OrganizationApplicationErrors.MutationOperationRequired);
        }

        if (!OrganizationLifecycleMutation.TryResolve(
                command.Action,
                out OrganizationLifecycleMutation mutation))
        {
            return Result.Failure<OrganizationDto>(
                OrganizationApplicationErrors.OrganizationLifecycleActionInvalid);
        }

        await governance.AcquireExclusiveAsync(
            command.OrganizationId,
            cancellationToken).ConfigureAwait(false);

        Result<OrganizationMembership> owner = await OrganizationMembershipAuthorization.RequireOwnerAsync(
            organizations, command.OrganizationId, command.SubjectId, cancellationToken).ConfigureAwait(false);
        if (owner.IsFailure)
        {
            return Result.Failure<OrganizationDto>(owner.Error);
        }

        Organization? organization = await organizations
            .GetOrganizationAsync(command.OrganizationId, cancellationToken)
            .ConfigureAwait(false);
        if (organization is null)
        {
            return Result.Failure<OrganizationDto>(OrganizationApplicationErrors.OrganizationNotFound);
        }

        Result<OrganizationActorId> actor = OrganizationActorId.Create(
            command.ActorId);
        if (actor.IsFailure)
        {
            return Result.Failure<OrganizationDto>(actor.Error);
        }

        if (organization.HasLastMutationOperation(command.OperationId))
        {
            return mutation.IsExactReplay(
                organization,
                command.OperationId,
                command.ExpectedVersion,
                actor.Value.Value)
                ? Result.Success(organization.ToDto())
                : Result.Failure<OrganizationDto>(
                    OrganizationApplicationErrors.MutationOperationConflict);
        }

        if (organization.Version != command.ExpectedVersion)
        {
            return Result.Failure<OrganizationDto>(
                OrganizationApplicationErrors.VersionConflict);
        }

        Result admitted = await mutationAdmission.AuthorizeAsync(
            new OrganizationMutationAdmissionContext(
                mutation.AdmissionOperation,
                command.OrganizationId,
                command.SubjectId),
            cancellationToken).ConfigureAwait(false);
        if (admitted.IsFailure)
        {
            return Result.Failure<OrganizationDto>(admitted.Error);
        }

        Guid eventId = ids.NewId();
        DateTimeOffset nowUtc = clock.UtcNow;
        Result changed = mutation.Apply(
            organization,
            command.ExpectedVersion,
            actor.Value.Value,
            command.OperationId,
            eventId,
            nowUtc);

        return changed.IsSuccess
            ? Result.Success(organization.ToDto())
            : Result.Failure<OrganizationDto>(changed.Error);
    }
}
