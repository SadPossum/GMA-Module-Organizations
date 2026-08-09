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
using Gma.Modules.Organizations.Domain.ValueObjects;

internal sealed class ChangeOrganizationLifecycleForAdministrationCommandHandler(
    IOrganizationRepository organizations,
    IOrganizationGovernanceCoordinator governance,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<ChangeOrganizationLifecycleForAdministrationCommand, OrganizationDto>
{
    public async Task<Result<OrganizationDto>> HandleAsync(
        ChangeOrganizationLifecycleForAdministrationCommand command,
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

        Organization? organization = await organizations.GetOrganizationAsync(
            command.OrganizationId, cancellationToken).ConfigureAwait(false);
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
