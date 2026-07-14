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
using Gma.Modules.Organizations.Domain.ValueObjects;
using DomainMembershipRole = Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;

internal sealed class CreateOrganizationCommandHandler(
    IOrganizationRepository organizations,
    IOrganizationAdmissionPolicy admissionPolicy,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<CreateOrganizationCommand, OrganizationMembershipSummaryDto>
{
    public async Task<Result<OrganizationMembershipSummaryDto>> HandleAsync(
        CreateOrganizationCommand command,
        CancellationToken cancellationToken)
    {
        Result admission = await admissionPolicy
            .CanCreateOrganizationAsync(command.SubjectId, cancellationToken)
            .ConfigureAwait(false);
        if (admission.IsFailure)
        {
            return Result.Failure<OrganizationMembershipSummaryDto>(admission.Error);
        }

        Result<OrganizationSlug> slug = OrganizationSlug.Create(command.Slug);
        if (slug.IsFailure)
        {
            return Result.Failure<OrganizationMembershipSummaryDto>(slug.Error);
        }

        if (await organizations.SlugExistsAsync(slug.Value.Value, null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<OrganizationMembershipSummaryDto>(OrganizationApplicationErrors.SlugConflict);
        }

        Guid organizationId = ids.NewId();
        DateTimeOffset nowUtc = clock.UtcNow;
        Result<Organization> organization = Organization.Create(
            organizationId, command.Name, slug.Value.Value, command.ActorId, ids.NewId(), nowUtc);
        if (organization.IsFailure)
        {
            return Result.Failure<OrganizationMembershipSummaryDto>(organization.Error);
        }

        Result<OrganizationMembership> membership = OrganizationMembership.Create(
            ids.NewId(), organizationId, command.SubjectId, DomainMembershipRole.Owner,
            command.ActorId, ids.NewId(), nowUtc);
        if (membership.IsFailure)
        {
            return Result.Failure<OrganizationMembershipSummaryDto>(membership.Error);
        }

        await organizations.AddOrganizationAsync(organization.Value, cancellationToken).ConfigureAwait(false);
        await organizations.AddMembershipAsync(membership.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(new OrganizationMembershipSummaryDto(
            organization.Value.ToDto(), membership.Value.ToDto()));
    }
}
