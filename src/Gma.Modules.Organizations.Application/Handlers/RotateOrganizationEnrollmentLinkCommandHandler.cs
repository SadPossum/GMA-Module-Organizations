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
using Microsoft.Extensions.Options;

internal sealed class RotateOrganizationEnrollmentLinkCommandHandler(
    IOrganizationRepository organizations,
    IOrganizationJoinSourceIssuanceCoordinator issuance,
    OrganizationJoinSourceAuthorization joinSourceAuthorization,
    OrganizationMutationAdmissionPolicy mutationAdmission,
    IOrganizationEnrollmentTokenService tokens,
    IOptions<OrganizationsOptions> options,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<
        RotateOrganizationEnrollmentLinkCommand,
        OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>>
{
    public async Task<Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>>> HandleAsync(
        RotateOrganizationEnrollmentLinkCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ReplacementSourceId == Guid.Empty)
        {
            return Failure(OrganizationApplicationErrors.JoinSourceIdRequired);
        }

        if (command.ReplacementSourceId == command.EnrollmentLinkId)
        {
            return Failure(OrganizationApplicationErrors.JoinSourceIssuanceConflict);
        }

        Result<OrganizationSubjectId> subject = OrganizationSubjectId.Create(command.SubjectId);
        Result<OrganizationActorId> actor = OrganizationActorId.Create(command.ActorId);
        Result<int> lifetime = OrganizationEnrollmentHandlerSupport.ResolveLifetimeHours(
            command.ReplacementLifetimeHours,
            options);
        if (subject.IsFailure || actor.IsFailure || lifetime.IsFailure)
        {
            return Failure(
                subject.IsFailure ? subject.Error :
                actor.IsFailure ? actor.Error : lifetime.Error);
        }

        Result authorized = await joinSourceAuthorization.AuthorizeAsync(
            new OrganizationJoinSourceAuthorizationContext(
                OrganizationJoinSourceAuthorizationOperation.RotateEnrollmentLink,
                command.OrganizationId,
                subject.Value.Value,
                command.EnrollmentLinkId),
            cancellationToken).ConfigureAwait(false);
        if (authorized.IsFailure)
        {
            return Failure(authorized.Error);
        }

        Organization? organization = await organizations.GetOrganizationAsync(
            command.OrganizationId,
            cancellationToken).ConfigureAwait(false);
        if (organization is not { Status: OrganizationState.Active })
        {
            return Failure(
                organization is null
                    ? OrganizationApplicationErrors.OrganizationNotFound
                    : Gma.Modules.Organizations.Domain.Errors.OrganizationDomainErrors.OrganizationNotActive);
        }

        await issuance.AcquireReplacementAsync(
            command.EnrollmentLinkId,
            command.ReplacementSourceId,
            cancellationToken).ConfigureAwait(false);

        DateTimeOffset nowUtc = clock.UtcNow;
        OrganizationEnrollmentLink? replacement = await organizations.GetEnrollmentLinkAsync(
            command.OrganizationId,
            command.ReplacementSourceId,
            cancellationToken).ConfigureAwait(false);
        if (replacement is not null)
        {
            bool exactReplay =
                replacement.ReplacesEnrollmentLinkId == command.EnrollmentLinkId &&
                replacement.ReplacesEnrollmentLinkVersion == command.ExpectedVersion &&
                string.Equals(
                    replacement.CreatorSubjectId,
                    subject.Value.Value,
                    StringComparison.Ordinal) &&
                string.Equals(replacement.CreatedBy, actor.Value.Value, StringComparison.Ordinal) &&
                replacement.ExpiresAtUtc == replacement.CreatedAtUtc.AddHours(lifetime.Value);
            return exactReplay
                ? Result.Success(new OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>(
                    replacement.ToDto(nowUtc),
                    OrganizationJoinSourceIssuanceOutcome.AlreadyIssued,
                    null,
                    null))
                : Failure(OrganizationApplicationErrors.JoinSourceIssuanceConflict);
        }

        if (await organizations.EnrollmentLinkIdExistsAsync(
                command.ReplacementSourceId,
                cancellationToken).ConfigureAwait(false) ||
            await organizations.InvitationIdExistsAsync(
                command.ReplacementSourceId,
                cancellationToken).ConfigureAwait(false))
        {
            return Failure(OrganizationApplicationErrors.JoinSourceIssuanceConflict);
        }

        OrganizationEnrollmentLink? predecessor = await organizations.GetEnrollmentLinkAsync(
            command.OrganizationId,
            command.EnrollmentLinkId,
            cancellationToken).ConfigureAwait(false);
        if (predecessor is null)
        {
            return Failure(OrganizationApplicationErrors.EnrollmentLinkNotFound);
        }

        Result admitted = await mutationAdmission.AuthorizeAsync(
            new OrganizationMutationAdmissionContext(
                OrganizationMutationAdmissionOperation.RotateEnrollmentLink,
                command.OrganizationId,
                subject.Value.Value,
                command.EnrollmentLinkId),
            cancellationToken).ConfigureAwait(false);
        if (admitted.IsFailure)
        {
            return Failure(admitted.Error);
        }

        Result rotated = predecessor.Rotate(
            command.ExpectedVersion,
            actor.Value.Value,
            ids.NewId(),
            nowUtc);
        if (rotated.IsFailure)
        {
            return Failure(rotated.Error);
        }

        IssuedOrganizationEnrollmentToken issued = tokens.Issue();
        Result<OrganizationEnrollmentLink> created = OrganizationEnrollmentLink.Create(
            command.ReplacementSourceId,
            predecessor.OrganizationId,
            subject.Value.Value,
            issued.Digest,
            nowUtc.AddHours(lifetime.Value),
            predecessor.MaximumClaims,
            predecessor.ApprovalMode,
            actor.Value.Value,
            ids.NewId(),
            nowUtc,
            predecessor.Id,
            command.ExpectedVersion);
        if (created.IsFailure)
        {
            return Failure(created.Error);
        }

        await organizations.AddEnrollmentLinkAsync(
            created.Value,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(new OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>(
            created.Value.ToDto(nowUtc),
            OrganizationJoinSourceIssuanceOutcome.Issued,
            issued.Secret,
            null));
    }

    private static Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>> Failure(
        Error error) => Result.Failure<
            OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>>(error);
}
