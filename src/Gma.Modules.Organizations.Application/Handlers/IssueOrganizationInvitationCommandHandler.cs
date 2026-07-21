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

internal sealed class IssueOrganizationInvitationCommandHandler(
    IOrganizationRepository organizations,
    IOrganizationInvitationTokenService tokens,
    IOptions<OrganizationsOptions> options,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<
        IssueOrganizationInvitationCommand,
        OrganizationJoinSourceIssuance<OrganizationInvitationDto>>
{
    public async Task<Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>>> HandleAsync(
        IssueOrganizationInvitationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command.Request);
        OrganizationInvitationIssuanceRequest request = command.Request;
        if (request.SourceId == Guid.Empty)
        {
            return Failure(OrganizationApplicationErrors.JoinSourceIdRequired);
        }

        Result<OrganizationSubjectId> subject = OrganizationSubjectId.Create(request.SubjectId);
        Result<OrganizationActorId> actor = OrganizationActorId.Create(request.ActorId);
        Result<OrganizationInvitationRecipient> recipient =
            OrganizationInvitationRecipient.Create(request.RecipientEmail);
        Result<int> lifetime =
            OrganizationInvitationHandlerSupport.ResolveLifetimeHours(request.LifetimeHours, options);
        if (subject.IsFailure || actor.IsFailure || recipient.IsFailure || lifetime.IsFailure)
        {
            return Failure(
                subject.IsFailure ? subject.Error : actor.IsFailure ? actor.Error :
                recipient.IsFailure ? recipient.Error : lifetime.Error);
        }

        Result<OrganizationMembership> owner = await OrganizationMembershipAuthorization.RequireOwnerAsync(
            organizations,
            request.OrganizationId,
            subject.Value.Value,
            cancellationToken).ConfigureAwait(false);
        if (owner.IsFailure)
        {
            return Failure(owner.Error);
        }

        Organization? organization = await organizations
            .GetOrganizationAsync(request.OrganizationId, cancellationToken)
            .ConfigureAwait(false);
        if (organization is not { Status: OrganizationState.Active })
        {
            return Failure(
                organization is null
                    ? OrganizationApplicationErrors.OrganizationNotFound
                    : Gma.Modules.Organizations.Domain.Errors.OrganizationDomainErrors.OrganizationNotActive);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        OrganizationInvitation? existing = await organizations.GetInvitationAsync(
            request.OrganizationId,
            request.SourceId,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            bool exactReplay =
                string.Equals(existing.InviterSubjectId, subject.Value.Value, StringComparison.Ordinal) &&
                string.Equals(existing.RecipientEmail, recipient.Value.Email, StringComparison.Ordinal) &&
                string.Equals(existing.CreatedBy, actor.Value.Value, StringComparison.Ordinal) &&
                existing.ExpiresAtUtc == existing.CreatedAtUtc.AddHours(lifetime.Value);
            return exactReplay
                ? Result.Success(new OrganizationJoinSourceIssuance<OrganizationInvitationDto>(
                    existing.ToDto(nowUtc),
                    OrganizationJoinSourceIssuanceOutcome.AlreadyIssued,
                    null,
                    null))
                : Failure(OrganizationApplicationErrors.JoinSourceIssuanceConflict);
        }

        if (await organizations.InvitationIdExistsAsync(request.SourceId, cancellationToken)
            .ConfigureAwait(false))
        {
            return Failure(OrganizationApplicationErrors.JoinSourceIssuanceConflict);
        }

        IssuedOrganizationInvitationToken issued = tokens.Issue();
        Result<OrganizationInvitation> invitation = OrganizationInvitation.Create(
            request.SourceId,
            organization.Id,
            subject.Value.Value,
            recipient.Value.Email,
            issued.Digest,
            nowUtc.AddHours(lifetime.Value),
            actor.Value.Value,
            ids.NewId(),
            nowUtc);
        if (invitation.IsFailure)
        {
            return Failure(invitation.Error);
        }

        await organizations.AddInvitationAsync(invitation.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(new OrganizationJoinSourceIssuance<OrganizationInvitationDto>(
            invitation.Value.ToDto(nowUtc),
            OrganizationJoinSourceIssuanceOutcome.Issued,
            issued.Secret,
            null));
    }

    private static Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> Failure(
        Error error) => Result.Failure<OrganizationJoinSourceIssuance<OrganizationInvitationDto>>(error);
}
