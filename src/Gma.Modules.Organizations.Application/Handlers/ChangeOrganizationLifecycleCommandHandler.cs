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

internal sealed class ChangeOrganizationLifecycleCommandHandler(
    IOrganizationRepository organizations,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<ChangeOrganizationLifecycleCommand, OrganizationDto>
{
    public async Task<Result<OrganizationDto>> HandleAsync(
        ChangeOrganizationLifecycleCommand command,
        CancellationToken cancellationToken)
    {
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

        Guid eventId = ids.NewId();
        DateTimeOffset nowUtc = clock.UtcNow;
        Result changed = command.Action switch
        {
            OrganizationLifecycleAction.Suspend => organization.Suspend(
                command.ExpectedVersion, command.ActorId, eventId, nowUtc),
            OrganizationLifecycleAction.Reactivate => organization.Reactivate(
                command.ExpectedVersion, command.ActorId, eventId, nowUtc),
            OrganizationLifecycleAction.Archive => organization.Archive(
                command.ExpectedVersion, command.ActorId, eventId, nowUtc),
            _ => Result.Failure(OrganizationApplicationErrors.OrganizationNotFound)
        };

        return changed.IsSuccess
            ? Result.Success(organization.ToDto())
            : Result.Failure<OrganizationDto>(changed.Error);
    }
}
