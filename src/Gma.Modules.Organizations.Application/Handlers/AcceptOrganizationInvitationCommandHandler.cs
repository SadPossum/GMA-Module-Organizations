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
using DomainMembershipRole = Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;

internal sealed class AcceptOrganizationInvitationCommandHandler(
    IOrganizationRepository organizations,
    IOrganizationInvitationTokenService tokens,
    IOrganizationInvitationAdmissionPolicy admissionPolicy,
    OrganizationJoinAdmissionPolicy joinAdmissionPolicy,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<AcceptOrganizationInvitationCommand, OrganizationInvitationAcceptanceDto>
{
    public async Task<Result<OrganizationInvitationAcceptanceDto>> HandleAsync(
        AcceptOrganizationInvitationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tokens.IsWellFormed(command.Token))
        {
            return Result.Failure<OrganizationInvitationAcceptanceDto>(
                OrganizationApplicationErrors.InvitationTokenInvalid);
        }

        string digest = tokens.ComputeDigest(command.Token);
        OrganizationInvitation? invitation = await organizations
            .GetInvitationByDigestAsync(digest, cancellationToken)
            .ConfigureAwait(false);
        if (invitation is null || !tokens.Verify(command.Token, invitation.TokenDigest))
        {
            return Result.Failure<OrganizationInvitationAcceptanceDto>(
                OrganizationApplicationErrors.InvitationTokenInvalid);
        }

        Organization? organization = await organizations
            .GetOrganizationAsync(invitation.OrganizationId, cancellationToken)
            .ConfigureAwait(false);
        if (organization is not { Status: OrganizationState.Active })
        {
            return Result.Failure<OrganizationInvitationAcceptanceDto>(
                organization is null ? OrganizationApplicationErrors.OrganizationNotFound :
                Gma.Modules.Organizations.Domain.Errors.OrganizationDomainErrors.OrganizationNotActive);
        }

        Result admission = await admissionPolicy.CanAcceptInvitationAsync(
            command.SubjectId, invitation.RecipientEmail, cancellationToken).ConfigureAwait(false);
        if (admission.IsFailure)
        {
            return Result.Failure<OrganizationInvitationAcceptanceDto>(admission.Error);
        }

        OrganizationMembership? membership = await organizations.GetMembershipAsync(
            organization.Id, command.SubjectId, cancellationToken).ConfigureAwait(false);
        if (invitation.Status == OrganizationInvitationState.Accepted)
        {
            return CreateIdempotentResult(invitation, organization, membership, command.SubjectId, clock.UtcNow);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result acceptable = invitation.EnsureAcceptable(command.SubjectId, nowUtc);
        if (acceptable.IsFailure)
        {
            return Result.Failure<OrganizationInvitationAcceptanceDto>(acceptable.Error);
        }

        bool productReady = await joinAdmissionPolicy.IsAllowedAsync(
            new OrganizationJoinAdmissionContext(
                OrganizationJoinAdmissionOperation.AcceptInvitation,
                organization.Id,
                invitation.Id,
                null,
                command.SubjectId,
                command.SubjectId,
                null),
            cancellationToken).ConfigureAwait(false);
        if (!productReady)
        {
            return Result.Failure<OrganizationInvitationAcceptanceDto>(
                OrganizationApplicationErrors.JoinAdmissionRejected);
        }

        if (membership is null)
        {
            Result<OrganizationMembership> created = OrganizationMembership.Create(
                ids.NewId(), organization.Id, command.SubjectId, DomainMembershipRole.Member,
                command.ActorId, ids.NewId(), nowUtc);
            if (created.IsFailure)
            {
                return Result.Failure<OrganizationInvitationAcceptanceDto>(created.Error);
            }

            membership = created.Value;
            await organizations.AddMembershipAsync(membership, cancellationToken).ConfigureAwait(false);
        }
        else if (membership.Status != OrganizationMembershipState.Active)
        {
            Result restored = membership.RestoreAsMember(
                membership.Version, command.ActorId, ids.NewId(), nowUtc);
            if (restored.IsFailure)
            {
                return Result.Failure<OrganizationInvitationAcceptanceDto>(restored.Error);
            }
        }

        Result accepted = invitation.Accept(
            command.SubjectId, membership.Id, command.ActorId, ids.NewId(), nowUtc);
        return accepted.IsSuccess
            ? Result.Success(ToAcceptance(invitation, organization, membership, nowUtc))
            : Result.Failure<OrganizationInvitationAcceptanceDto>(accepted.Error);
    }

    private static Result<OrganizationInvitationAcceptanceDto> CreateIdempotentResult(
        OrganizationInvitation invitation,
        Organization organization,
        OrganizationMembership? membership,
        string subjectId,
        DateTimeOffset nowUtc)
    {
        bool sameSubject = string.Equals(invitation.AcceptedSubjectId, subjectId, StringComparison.Ordinal);
        bool sameMembership = membership is not null && invitation.AcceptedMembershipId == membership.Id;
        return sameSubject && sameMembership
            ? Result.Success(ToAcceptance(invitation, organization, membership!, nowUtc))
            : Result.Failure<OrganizationInvitationAcceptanceDto>(
                Gma.Modules.Organizations.Domain.Errors.OrganizationDomainErrors.InvitationClaimedByAnotherSubject);
    }

    private static OrganizationInvitationAcceptanceDto ToAcceptance(
        OrganizationInvitation invitation,
        Organization organization,
        OrganizationMembership membership,
        DateTimeOffset nowUtc) => new(
        invitation.ToDto(nowUtc),
        new OrganizationMembershipSummaryDto(organization.ToDto(), membership.ToDto()));
}
