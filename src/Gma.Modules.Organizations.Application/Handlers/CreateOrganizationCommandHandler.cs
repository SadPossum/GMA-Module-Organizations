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
using Gma.Modules.Organizations.Domain.ValueObjects;
using DomainMembershipRole = Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;

internal sealed class CreateOrganizationCommandHandler(
    IOrganizationRepository organizations,
    IOrganizationCreationCoordinator creation,
    OrganizationCreationAdmissionPolicy admissionPolicy,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<CreateOrganizationCommand, OrganizationMembershipSummaryDto>
{
    public async Task<Result<OrganizationMembershipSummaryDto>> HandleAsync(
        CreateOrganizationCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty)
        {
            return Failure(
                OrganizationApplicationErrors.CreationOperationRequired);
        }

        Result<OrganizationName> name = OrganizationName.Create(command.Name);
        Result<OrganizationSlug> slug = OrganizationSlug.Create(command.Slug);
        Result<OrganizationSubjectId> subject =
            OrganizationSubjectId.Create(command.SubjectId);
        Result<OrganizationActorId> actor =
            OrganizationActorId.Create(command.ActorId);
        if (name.IsFailure || slug.IsFailure || subject.IsFailure ||
            actor.IsFailure)
        {
            return Failure(
                name.IsFailure ? name.Error :
                slug.IsFailure ? slug.Error :
                subject.IsFailure ? subject.Error : actor.Error);
        }

        string fingerprint = OrganizationCreationFingerprint.Compute(
            name.Value.Value,
            slug.Value.Value,
            subject.Value.Value,
            actor.Value.Value);
        Organization? existing = await creation.AcquireAsync(
            command.OperationId,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            OrganizationMembership? existingMembership = await organizations
                .GetMembershipAsync(
                    existing.Id,
                    subject.Value.Value,
                    cancellationToken).ConfigureAwait(false);
            bool exactReplay = string.Equals(
                    existing.CreationRequestFingerprint,
                    fingerprint,
                    StringComparison.Ordinal) &&
                existingMembership?.Status ==
                    OrganizationMembershipState.Active;
            return exactReplay
                ? Result.Success(new OrganizationMembershipSummaryDto(
                    existing.ToDto(),
                    existingMembership!.ToDto()))
                : Failure(
                    OrganizationApplicationErrors.CreationOperationConflict);
        }

        Result admission = await admissionPolicy.AuthorizeAsync(
            new OrganizationCreationAdmissionRequest(
                command.OperationId,
                name.Value.Value,
                slug.Value.Value,
                subject.Value.Value,
                actor.Value.Value),
            cancellationToken).ConfigureAwait(false);
        if (admission.IsFailure)
        {
            return Failure(admission.Error);
        }

        if (await organizations.SlugExistsAsync(slug.Value.Value, null, cancellationToken).ConfigureAwait(false))
        {
            return Failure(OrganizationApplicationErrors.SlugConflict);
        }

        DateTimeOffset nowUtc = Canonicalize(clock.UtcNow);
        Result<Organization> organization = Organization.Create(
            command.OperationId,
            name.Value.Value,
            slug.Value.Value,
            actor.Value.Value,
            ids.NewId(),
            nowUtc,
            fingerprint);
        if (organization.IsFailure)
        {
            return Failure(organization.Error);
        }

        Result<OrganizationMembership> membership = OrganizationMembership.Create(
            ids.NewId(),
            organization.Value.Id,
            subject.Value.Value,
            DomainMembershipRole.Owner,
            actor.Value.Value,
            ids.NewId(),
            nowUtc);
        if (membership.IsFailure)
        {
            return Failure(membership.Error);
        }

        await organizations.AddOrganizationAsync(organization.Value, cancellationToken).ConfigureAwait(false);
        await organizations.AddMembershipAsync(membership.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(new OrganizationMembershipSummaryDto(
            organization.Value.ToDto(), membership.Value.ToDto()));
    }

    private static Result<OrganizationMembershipSummaryDto> Failure(
        Error error) =>
        Result.Failure<OrganizationMembershipSummaryDto>(error);

    private static DateTimeOffset Canonicalize(DateTimeOffset value)
    {
        DateTimeOffset utc = value.ToUniversalTime();
        const long ticksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        return new(
            utc.Ticks - (utc.Ticks % ticksPerMicrosecond),
            TimeSpan.Zero);
    }
}
