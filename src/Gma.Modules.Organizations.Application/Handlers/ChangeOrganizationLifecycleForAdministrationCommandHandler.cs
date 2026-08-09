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
        await governance.AcquireExclusiveAsync(
            command.OrganizationId,
            cancellationToken).ConfigureAwait(false);

        Organization? organization = await organizations.GetOrganizationAsync(
            command.OrganizationId, cancellationToken).ConfigureAwait(false);
        if (organization is null)
        {
            return Result.Failure<OrganizationDto>(OrganizationApplicationErrors.OrganizationNotFound);
        }

        Result changed = command.Action switch
        {
            OrganizationLifecycleAction.Suspend => organization.Suspend(
                command.ExpectedVersion, command.ActorId, ids.NewId(), clock.UtcNow),
            OrganizationLifecycleAction.Reactivate => organization.Reactivate(
                command.ExpectedVersion, command.ActorId, ids.NewId(), clock.UtcNow),
            OrganizationLifecycleAction.Archive => organization.Archive(
                command.ExpectedVersion, command.ActorId, ids.NewId(), clock.UtcNow),
            _ => Result.Failure(OrganizationApplicationErrors.OrganizationLifecycleActionInvalid)
        };
        return changed.IsSuccess
            ? Result.Success(organization.ToDto())
            : Result.Failure<OrganizationDto>(changed.Error);
    }
}
