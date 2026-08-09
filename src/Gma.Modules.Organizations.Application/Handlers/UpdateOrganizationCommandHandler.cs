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

internal sealed class UpdateOrganizationCommandHandler(
    IOrganizationRepository organizations,
    IOrganizationGovernanceCoordinator governance,
    OrganizationMutationAdmissionPolicy mutationAdmission,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<UpdateOrganizationCommand, OrganizationDto>
{
    public async Task<Result<OrganizationDto>> HandleAsync(
        UpdateOrganizationCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<OrganizationDto>(
                OrganizationApplicationErrors.MutationOperationRequired);
        }

        await governance.AcquireSharedAsync(
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

        Result<OrganizationName> name = OrganizationName.Create(command.Name);
        if (name.IsFailure)
        {
            return Result.Failure<OrganizationDto>(name.Error);
        }

        Result<OrganizationSlug> slug = OrganizationSlug.Create(command.Slug);
        if (slug.IsFailure)
        {
            return Result.Failure<OrganizationDto>(slug.Error);
        }

        Result<OrganizationActorId> actor = OrganizationActorId.Create(
            command.ActorId);
        if (actor.IsFailure)
        {
            return Result.Failure<OrganizationDto>(actor.Error);
        }

        if (organization.HasLastMutationOperation(command.OperationId))
        {
            return organization.IsExactProfileMutationReplay(
                command.OperationId,
                command.ExpectedVersion,
                actor.Value.Value,
                name.Value.Value,
                slug.Value.Value)
                ? Result.Success(organization.ToDto())
                : Result.Failure<OrganizationDto>(
                    OrganizationApplicationErrors.MutationOperationConflict);
        }

        if (organization.Version != command.ExpectedVersion)
        {
            return Result.Failure<OrganizationDto>(
                OrganizationApplicationErrors.VersionConflict);
        }

        if (await organizations.SlugExistsAsync(
            slug.Value.Value, organization.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<OrganizationDto>(OrganizationApplicationErrors.SlugConflict);
        }

        Result admitted = await mutationAdmission.AuthorizeAsync(
            new OrganizationMutationAdmissionContext(
                OrganizationMutationAdmissionOperation.UpdateOrganization,
                command.OrganizationId,
                command.SubjectId),
            cancellationToken).ConfigureAwait(false);
        if (admitted.IsFailure)
        {
            return Result.Failure<OrganizationDto>(admitted.Error);
        }

        Result changed = organization.UpdateProfile(
            name.Value.Value,
            slug.Value.Value,
            command.ExpectedVersion,
            actor.Value.Value,
            command.OperationId,
            ids.NewId(),
            clock.UtcNow);
        return changed.IsSuccess
            ? Result.Success(organization.ToDto())
            : Result.Failure<OrganizationDto>(changed.Error);
    }
}
