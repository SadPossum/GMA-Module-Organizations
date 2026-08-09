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

internal sealed class ResolveOrganizationJoinRequestCommandHandler(
    IOrganizationRepository organizations,
    IOrganizationGovernanceCoordinator governance,
    IOrganizationJoinSubjectCoordinator joinSubjects,
    OrganizationJoinSourceAuthorization joinSourceAuthorization,
    OrganizationJoinAdmissionPolicy joinAdmissionPolicy,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<ResolveOrganizationJoinRequestCommand, OrganizationEnrollmentOutcomeDto>
{
    public async Task<Result<OrganizationEnrollmentOutcomeDto>> HandleAsync(
        ResolveOrganizationJoinRequestCommand command,
        CancellationToken cancellationToken)
    {
        await governance.AcquireSharedAsync(
            command.OrganizationId,
            cancellationToken).ConfigureAwait(false);

        Result authorized = await joinSourceAuthorization.AuthorizeAsync(
            new OrganizationJoinSourceAuthorizationContext(
                OrganizationJoinSourceAuthorizationOperation.ResolveJoinRequest,
                command.OrganizationId,
                command.SubjectId,
                ClaimId: command.ClaimId),
            cancellationToken).ConfigureAwait(false);
        if (authorized.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(
                authorized.Error);
        }

        OrganizationEnrollmentClaim? claim = await organizations.GetEnrollmentClaimAsync(
            command.OrganizationId, command.ClaimId, cancellationToken).ConfigureAwait(false);
        if (claim is null)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(
                OrganizationApplicationErrors.EnrollmentClaimNotFound);
        }

        OrganizationEnrollmentLink? link = await organizations.GetEnrollmentLinkAsync(
            command.OrganizationId, claim.EnrollmentLinkId, cancellationToken).ConfigureAwait(false);
        if (link is null)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(
                OrganizationApplicationErrors.EnrollmentLinkNotFound);
        }

        if (command.Decision is not OrganizationJoinRequestDecision.Approve and
            not OrganizationJoinRequestDecision.Reject)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(
                OrganizationApplicationErrors.EnrollmentDecisionInvalid);
        }

        if (IsExactTerminalReplay(command, claim))
        {
            return command.Decision == OrganizationJoinRequestDecision.Reject
                ? Result.Success(new OrganizationEnrollmentOutcomeDto(claim.ToDto(), null))
                : await this.ReplayApprovalAsync(
                    command.OrganizationId,
                    claim,
                    cancellationToken).ConfigureAwait(false);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        if (claim.IsDecisionDue(nowUtc))
        {
            return OrganizationEnrollmentClaimExpiry.Expire(
                claim, link, command.ExpectedClaimVersion, nowUtc, ids);
        }

        return command.Decision switch
        {
            OrganizationJoinRequestDecision.Approve => await this.ApproveAsync(
                command, claim, link, nowUtc, cancellationToken).ConfigureAwait(false),
            _ => this.Reject(command, claim, link, nowUtc)
        };
    }

    private async Task<Result<OrganizationEnrollmentOutcomeDto>> ApproveAsync(
        ResolveOrganizationJoinRequestCommand command,
        OrganizationEnrollmentClaim claim,
        OrganizationEnrollmentLink link,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (claim.Version != command.ExpectedClaimVersion)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(
                Gma.Modules.Organizations.Domain.Errors.OrganizationDomainErrors.VersionConflict);
        }

        if (claim.Status != OrganizationEnrollmentClaimState.Pending)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(
                Gma.Modules.Organizations.Domain.Errors.OrganizationDomainErrors.EnrollmentClaimUnavailable);
        }

        await joinSubjects.AcquireAsync(
            command.OrganizationId,
            claim.SubjectId,
            cancellationToken).ConfigureAwait(false);

        Organization? organization = await organizations.GetOrganizationAsync(
            command.OrganizationId, cancellationToken).ConfigureAwait(false);
        if (organization is not { Status: OrganizationState.Active })
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(organization is null
                ? OrganizationApplicationErrors.OrganizationNotFound
                : Gma.Modules.Organizations.Domain.Errors.OrganizationDomainErrors.OrganizationNotActive);
        }

        OrganizationMembership? existingMembership = await organizations.GetMembershipAsync(
            organization.Id,
            claim.SubjectId,
            cancellationToken).ConfigureAwait(false);
        if (existingMembership is { Status: OrganizationMembershipState.Active })
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(
                OrganizationApplicationErrors.MembershipConflict);
        }

        Result productAdmission = await joinAdmissionPolicy.AuthorizeAsync(
            new OrganizationJoinAdmissionContext(
                OrganizationJoinAdmissionOperation.ApproveEnrollment,
                organization.Id,
                link.Id,
                claim.Id,
                claim.SubjectId,
                command.SubjectId,
                OrganizationMappings.MapMode(link.ApprovalMode)),
            cancellationToken).ConfigureAwait(false);
        if (productAdmission.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(
                productAdmission.Error);
        }

        Result<OrganizationMembership> membership = await OrganizationMemberProvisioning.EnsureActiveMemberAsync(
            organizations, existingMembership, organization.Id, claim.SubjectId, command.ActorId,
            nowUtc, ids, cancellationToken).ConfigureAwait(false);
        if (membership.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(membership.Error);
        }

        Result approved = claim.Approve(
            membership.Value.Id, command.ExpectedClaimVersion,
            command.ActorId, ids.NewId(), nowUtc);
        return approved.IsSuccess
            ? Result.Success(ToOutcome(claim, organization, membership.Value))
            : Result.Failure<OrganizationEnrollmentOutcomeDto>(approved.Error);
    }

    private async Task<Result<OrganizationEnrollmentOutcomeDto>> ReplayApprovalAsync(
        Guid organizationId,
        OrganizationEnrollmentClaim claim,
        CancellationToken cancellationToken)
    {
        Organization? organization = await organizations.GetOrganizationAsync(
            organizationId, cancellationToken).ConfigureAwait(false);
        if (organization is not { Status: OrganizationState.Active })
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(organization is null
                ? OrganizationApplicationErrors.OrganizationNotFound
                : Gma.Modules.Organizations.Domain.Errors.OrganizationDomainErrors.OrganizationNotActive);
        }

        OrganizationMembership? membership = await organizations.GetMembershipAsync(
            organizationId, claim.SubjectId, cancellationToken).ConfigureAwait(false);
        return membership is { Status: OrganizationMembershipState.Active } &&
               claim.MembershipId == membership.Id
            ? Result.Success(ToOutcome(claim, organization, membership))
            : Result.Failure<OrganizationEnrollmentOutcomeDto>(
                OrganizationApplicationErrors.MembershipConflict);
    }

    private Result<OrganizationEnrollmentOutcomeDto> Reject(
        ResolveOrganizationJoinRequestCommand command,
        OrganizationEnrollmentClaim claim,
        OrganizationEnrollmentLink link,
        DateTimeOffset nowUtc)
    {
        Result rejected = claim.Reject(
            command.ExpectedClaimVersion, command.ActorId, ids.NewId(), nowUtc);
        if (rejected.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(rejected.Error);
        }

        if (link.Status != OrganizationEnrollmentLinkState.Active || link.ReservedClaims == 0)
        {
            return Result.Success(new OrganizationEnrollmentOutcomeDto(claim.ToDto(), null));
        }

        Result released = link.ReleaseClaim(
            link.Version, command.ActorId, ids.NewId(), nowUtc);
        return released.IsSuccess
            ? Result.Success(new OrganizationEnrollmentOutcomeDto(claim.ToDto(), null))
            : Result.Failure<OrganizationEnrollmentOutcomeDto>(released.Error);
    }

    private static bool IsExactTerminalReplay(
        ResolveOrganizationJoinRequestCommand command,
        OrganizationEnrollmentClaim claim) =>
        claim.Version > 1 &&
        command.ExpectedClaimVersion == claim.Version - 1 &&
        ((command.Decision == OrganizationJoinRequestDecision.Approve &&
          claim.Status == OrganizationEnrollmentClaimState.Accepted) ||
         (command.Decision == OrganizationJoinRequestDecision.Reject &&
          claim.Status == OrganizationEnrollmentClaimState.Rejected));

    private static OrganizationEnrollmentOutcomeDto ToOutcome(
        OrganizationEnrollmentClaim claim,
        Organization organization,
        OrganizationMembership membership) => new(
        claim.ToDto(),
        new OrganizationMembershipSummaryDto(organization.ToDto(), membership.ToDto()));
}
