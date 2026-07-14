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
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<UpdateOrganizationCommand, OrganizationDto>
{
    public async Task<Result<OrganizationDto>> HandleAsync(
        UpdateOrganizationCommand command,
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

        Result<OrganizationSlug> slug = OrganizationSlug.Create(command.Slug);
        if (slug.IsFailure)
        {
            return Result.Failure<OrganizationDto>(slug.Error);
        }

        if (await organizations.SlugExistsAsync(
            slug.Value.Value, organization.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<OrganizationDto>(OrganizationApplicationErrors.SlugConflict);
        }

        Result changed = organization.UpdateProfile(
            command.Name, slug.Value.Value, command.ExpectedVersion,
            command.ActorId, ids.NewId(), clock.UtcNow);
        return changed.IsSuccess
            ? Result.Success(organization.ToDto())
            : Result.Failure<OrganizationDto>(changed.Error);
    }
}
