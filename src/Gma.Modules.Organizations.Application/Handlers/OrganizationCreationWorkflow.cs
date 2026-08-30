namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Domain.ValueObjects;
using DomainMembershipRole = Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;

internal sealed class OrganizationCreationWorkflow(
    IOrganizationRepository organizations,
    IOrganizationCreationCoordinator creation,
    ISystemClock clock,
    IIdGenerator ids)
{
    public async Task<Result<OrganizationCreationWorkflowResult>> ExecuteAsync(
        Guid operationId,
        string name,
        string slug,
        string subjectId,
        string actorId,
        Func<NormalizedOrganizationCreation, string> fingerprintFactory,
        Func<NormalizedOrganizationCreation, CancellationToken, ValueTask<Result>>
            authorizeFreshAsync,
        OrganizationCreationReplayMembership replayMembership,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprintFactory);
        ArgumentNullException.ThrowIfNull(authorizeFreshAsync);

        Result<NormalizedOrganizationCreation> normalized = Normalize(
            operationId,
            name,
            slug,
            subjectId,
            actorId);
        if (normalized.IsFailure)
        {
            return Failure(normalized.Error);
        }

        string fingerprint = fingerprintFactory(normalized.Value);
        OrganizationCreationAcquisition acquisition = await creation.AcquireAsync(
            operationId,
            cancellationToken).ConfigureAwait(false);
        if (acquisition.IsScopeClosed)
        {
            return Failure(OrganizationApplicationErrors.CreationOperationConflict);
        }

        Organization? existing = acquisition.Organization;
        if (existing is not null)
        {
            OrganizationMembership? existingMembership = await organizations
                .GetMembershipAsync(
                    existing.Id,
                    normalized.Value.SubjectId,
                    cancellationToken).ConfigureAwait(false);
            bool membershipCanReplay = existingMembership is not null &&
                replayMembership switch
                {
                    OrganizationCreationReplayMembership.Active =>
                        existingMembership.Status ==
                            OrganizationMembershipState.Active,
                    OrganizationCreationReplayMembership.Existing => true,
                    _ => false
                };
            bool exactReplay = membershipCanReplay && string.Equals(
                    existing.CreationRequestFingerprint,
                    fingerprint,
                    StringComparison.Ordinal);
            return exactReplay
                ? Result.Success(new OrganizationCreationWorkflowResult(
                    new OrganizationMembershipSummaryDto(
                        existing.ToDto(),
                        existingMembership!.ToDto()),
                    WasCreated: false))
                : Failure(OrganizationApplicationErrors.CreationOperationConflict);
        }

        Result admission = await authorizeFreshAsync(
            normalized.Value,
            cancellationToken).ConfigureAwait(false);
        if (admission.IsFailure)
        {
            return Failure(admission.Error);
        }

        if (await organizations.SlugExistsAsync(
                normalized.Value.Slug,
                null,
                cancellationToken).ConfigureAwait(false))
        {
            return Failure(OrganizationApplicationErrors.SlugConflict);
        }

        DateTimeOffset nowUtc = Canonicalize(clock.UtcNow);
        Result<Organization> organization = Organization.Create(
            operationId,
            normalized.Value.Name,
            normalized.Value.Slug,
            normalized.Value.ActorId,
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
            normalized.Value.SubjectId,
            DomainMembershipRole.Owner,
            normalized.Value.ActorId,
            ids.NewId(),
            nowUtc);
        if (membership.IsFailure)
        {
            return Failure(membership.Error);
        }

        await organizations.AddOrganizationAsync(
            organization.Value,
            cancellationToken).ConfigureAwait(false);
        await organizations.AddMembershipAsync(
            membership.Value,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(new OrganizationCreationWorkflowResult(
            new OrganizationMembershipSummaryDto(
                organization.Value.ToDto(),
                membership.Value.ToDto()),
            WasCreated: true));
    }

    private static Result<NormalizedOrganizationCreation> Normalize(
        Guid operationId,
        string name,
        string slug,
        string subjectId,
        string actorId)
    {
        if (operationId == Guid.Empty)
        {
            return Result.Failure<NormalizedOrganizationCreation>(
                OrganizationApplicationErrors.CreationOperationRequired);
        }

        Result<OrganizationName> organizationName = OrganizationName.Create(name);
        Result<OrganizationSlug> organizationSlug = OrganizationSlug.Create(slug);
        Result<OrganizationSubjectId> subject =
            OrganizationSubjectId.Create(subjectId);
        Result<OrganizationActorId> actor = OrganizationActorId.Create(actorId);
        if (organizationName.IsFailure || organizationSlug.IsFailure ||
            subject.IsFailure || actor.IsFailure)
        {
            return Result.Failure<NormalizedOrganizationCreation>(
                organizationName.IsFailure ? organizationName.Error :
                organizationSlug.IsFailure ? organizationSlug.Error :
                subject.IsFailure ? subject.Error : actor.Error);
        }

        return Result.Success(new NormalizedOrganizationCreation(
            operationId,
            organizationName.Value.Value,
            organizationSlug.Value.Value,
            subject.Value.Value,
            actor.Value.Value));
    }

    private static Result<OrganizationCreationWorkflowResult> Failure(Error error) =>
        Result.Failure<OrganizationCreationWorkflowResult>(error);

    private static DateTimeOffset Canonicalize(DateTimeOffset value)
    {
        DateTimeOffset utc = value.ToUniversalTime();
        const long ticksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        return new(
            utc.Ticks - (utc.Ticks % ticksPerMicrosecond),
            TimeSpan.Zero);
    }
}

internal sealed record NormalizedOrganizationCreation(
    Guid OperationId,
    string Name,
    string Slug,
    string SubjectId,
    string ActorId);

internal sealed record OrganizationCreationWorkflowResult(
    OrganizationMembershipSummaryDto Summary,
    bool WasCreated);

internal enum OrganizationCreationReplayMembership
{
    Active = 1,
    Existing = 2
}
