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

internal sealed class ReissueOrganizationInvitationCommandHandler(
    IOrganizationRepository organizations,
    IOrganizationGovernanceCoordinator governance,
    IOrganizationJoinSourceIssuanceCoordinator issuance,
    OrganizationJoinSourceAuthorization joinSourceAuthorization,
    OrganizationMutationAdmissionPolicy mutationAdmission,
    IOrganizationInvitationTokenService tokens,
    IOptions<OrganizationsOptions> options,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<
        ReissueOrganizationInvitationCommand,
        OrganizationJoinSourceIssuance<OrganizationInvitationDto>>
{
    public async Task<Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>>> HandleAsync(
        ReissueOrganizationInvitationCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ReplacementSourceId == Guid.Empty)
        {
            return Failure(OrganizationApplicationErrors.JoinSourceIdRequired);
        }

        if (command.ReplacementSourceId == command.InvitationId)
        {
            return Failure(OrganizationApplicationErrors.JoinSourceIssuanceConflict);
        }

        Result<OrganizationSubjectId> subject = OrganizationSubjectId.Create(command.SubjectId);
        Result<OrganizationActorId> actor = OrganizationActorId.Create(command.ActorId);
        Result<int> lifetime = OrganizationInvitationHandlerSupport.ResolveLifetimeHours(
            command.LifetimeHours,
            options);
        if (subject.IsFailure || actor.IsFailure || lifetime.IsFailure)
        {
            return Failure(
                subject.IsFailure ? subject.Error :
                actor.IsFailure ? actor.Error : lifetime.Error);
        }

        await governance.AcquireSharedAsync(
            command.OrganizationId,
            cancellationToken).ConfigureAwait(false);

        Result authorized = await joinSourceAuthorization.AuthorizeAsync(
            new OrganizationJoinSourceAuthorizationContext(
                OrganizationJoinSourceAuthorizationOperation.ReissueInvitation,
                command.OrganizationId,
                subject.Value.Value,
                command.InvitationId),
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
            command.InvitationId,
            command.ReplacementSourceId,
            cancellationToken).ConfigureAwait(false);

        DateTimeOffset nowUtc = clock.UtcNow;
        OrganizationInvitation? replacement = await organizations.GetInvitationAsync(
            command.OrganizationId,
            command.ReplacementSourceId,
            cancellationToken).ConfigureAwait(false);
        if (replacement is not null)
        {
            bool exactReplay =
                replacement.ReplacesInvitationId == command.InvitationId &&
                replacement.ReplacesInvitationVersion == command.ExpectedVersion &&
                string.Equals(
                    replacement.InviterSubjectId,
                    subject.Value.Value,
                    StringComparison.Ordinal) &&
                string.Equals(replacement.CreatedBy, actor.Value.Value, StringComparison.Ordinal) &&
                replacement.ExpiresAtUtc == replacement.CreatedAtUtc.AddHours(lifetime.Value);
            return exactReplay
                ? Result.Success(new OrganizationJoinSourceIssuance<OrganizationInvitationDto>(
                    replacement.ToDto(nowUtc),
                    OrganizationJoinSourceIssuanceOutcome.AlreadyIssued,
                    null,
                    null))
                : Failure(OrganizationApplicationErrors.JoinSourceIssuanceConflict);
        }

        if (await organizations.InvitationIdExistsAsync(
                command.ReplacementSourceId,
                cancellationToken).ConfigureAwait(false) ||
            await organizations.EnrollmentLinkIdExistsAsync(
                command.ReplacementSourceId,
                cancellationToken).ConfigureAwait(false))
        {
            return Failure(OrganizationApplicationErrors.JoinSourceIssuanceConflict);
        }

        OrganizationInvitation? predecessor = await organizations.GetInvitationAsync(
            command.OrganizationId,
            command.InvitationId,
            cancellationToken).ConfigureAwait(false);
        if (predecessor is null)
        {
            return Failure(OrganizationApplicationErrors.InvitationNotFound);
        }

        Result admitted = await mutationAdmission.AuthorizeAsync(
            new OrganizationMutationAdmissionContext(
                OrganizationMutationAdmissionOperation.ReissueInvitation,
                command.OrganizationId,
                subject.Value.Value,
                command.InvitationId),
            cancellationToken).ConfigureAwait(false);
        if (admitted.IsFailure)
        {
            return Failure(admitted.Error);
        }

        Result superseded = predecessor.Supersede(
            command.ExpectedVersion,
            actor.Value.Value,
            ids.NewId(),
            nowUtc);
        if (superseded.IsFailure)
        {
            return Failure(superseded.Error);
        }

        IssuedOrganizationInvitationToken issued = tokens.Issue();
        Result<OrganizationInvitation> created = OrganizationInvitation.Create(
            command.ReplacementSourceId,
            predecessor.OrganizationId,
            subject.Value.Value,
            predecessor.RecipientEmail,
            issued.Digest,
            nowUtc.AddHours(lifetime.Value),
            actor.Value.Value,
            ids.NewId(),
            nowUtc,
            predecessor.Id,
            command.ExpectedVersion);
        if (created.IsFailure)
        {
            return Failure(created.Error);
        }

        await organizations.AddInvitationAsync(
            created.Value,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(new OrganizationJoinSourceIssuance<OrganizationInvitationDto>(
            created.Value.ToDto(nowUtc),
            OrganizationJoinSourceIssuanceOutcome.Issued,
            issued.Secret,
            null));
    }

    private static Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> Failure(
        Error error) => Result.Failure<
            OrganizationJoinSourceIssuance<OrganizationInvitationDto>>(error);
}
