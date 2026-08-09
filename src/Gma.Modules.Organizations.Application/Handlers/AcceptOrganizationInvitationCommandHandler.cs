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

internal sealed class AcceptOrganizationInvitationCommandHandler(
    IOrganizationRepository organizations,
    IOrganizationGovernanceCoordinator governance,
    IOrganizationJoinSubjectCoordinator joinSubjects,
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

        Result<OrganizationSubjectId> subject = OrganizationSubjectId.Create(command.SubjectId);
        if (subject.IsFailure)
        {
            return Result.Failure<OrganizationInvitationAcceptanceDto>(subject.Error);
        }

        await governance.AcquireSharedAsync(
            invitation.OrganizationId,
            cancellationToken).ConfigureAwait(false);
        await joinSubjects.AcquireAsync(
            invitation.OrganizationId,
            subject.Value.Value,
            cancellationToken).ConfigureAwait(false);

        Organization? organization = await organizations
            .GetOrganizationAsync(invitation.OrganizationId, cancellationToken)
            .ConfigureAwait(false);
        if (organization is not { Status: OrganizationState.Active })
        {
            return Result.Failure<OrganizationInvitationAcceptanceDto>(
                organization is null ? OrganizationApplicationErrors.OrganizationNotFound :
                Gma.Modules.Organizations.Domain.Errors.OrganizationDomainErrors.OrganizationNotActive);
        }

        OrganizationMembership? membership = await organizations.GetMembershipAsync(
            organization.Id, subject.Value.Value, cancellationToken).ConfigureAwait(false);
        DateTimeOffset nowUtc = clock.UtcNow;
        if (invitation.Status == OrganizationInvitationState.Accepted)
        {
            return CreateIdempotentResult(
                invitation,
                organization,
                membership,
                subject.Value.Value,
                nowUtc);
        }

        if (membership is { Status: OrganizationMembershipState.Active })
        {
            return Result.Failure<OrganizationInvitationAcceptanceDto>(
                OrganizationApplicationErrors.MembershipConflict);
        }

        if (await organizations.HasCurrentPendingEnrollmentClaimAsync(
                organization.Id,
                subject.Value.Value,
                nowUtc,
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<OrganizationInvitationAcceptanceDto>(
                OrganizationApplicationErrors.JoinRequestConflict);
        }

        Result acceptable = invitation.EnsureAcceptable(subject.Value.Value, nowUtc);
        if (acceptable.IsFailure)
        {
            return Result.Failure<OrganizationInvitationAcceptanceDto>(acceptable.Error);
        }

        Result admission = await admissionPolicy.CanAcceptInvitationAsync(
            subject.Value.Value, invitation.RecipientEmail, cancellationToken).ConfigureAwait(false);
        if (admission.IsFailure)
        {
            return Result.Failure<OrganizationInvitationAcceptanceDto>(admission.Error);
        }

        Result productAdmission = await joinAdmissionPolicy.AuthorizeAsync(
            new OrganizationJoinAdmissionContext(
                OrganizationJoinAdmissionOperation.AcceptInvitation,
                organization.Id,
                invitation.Id,
                null,
                subject.Value.Value,
                subject.Value.Value,
                null),
            cancellationToken).ConfigureAwait(false);
        if (productAdmission.IsFailure)
        {
            return Result.Failure<OrganizationInvitationAcceptanceDto>(
                productAdmission.Error);
        }

        if (membership is null)
        {
            Result<OrganizationMembership> created = OrganizationMembership.Create(
                ids.NewId(), organization.Id, subject.Value.Value, DomainMembershipRole.Member,
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
            subject.Value.Value, membership.Id, command.ActorId, ids.NewId(), nowUtc);
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
