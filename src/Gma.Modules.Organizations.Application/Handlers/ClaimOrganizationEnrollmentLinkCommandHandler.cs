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
using DomainApprovalMode = Gma.Modules.Organizations.Domain.Enums.OrganizationEnrollmentApprovalMode;

internal sealed class ClaimOrganizationEnrollmentLinkCommandHandler(
    IOrganizationRepository organizations,
    IOrganizationGovernanceCoordinator governance,
    IOrganizationJoinSubjectCoordinator joinSubjects,
    IOrganizationEnrollmentTokenService tokens,
    OrganizationJoinAdmissionPolicy joinAdmissionPolicy,
    ISystemClock clock,
    IIdGenerator ids,
    IOptions<OrganizationsOptions> options)
    : ICommandHandler<ClaimOrganizationEnrollmentLinkCommand, OrganizationEnrollmentOutcomeDto>
{
    public async Task<Result<OrganizationEnrollmentOutcomeDto>> HandleAsync(
        ClaimOrganizationEnrollmentLinkCommand command,
        CancellationToken cancellationToken)
    {
        if (!tokens.IsWellFormed(command.Token))
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(OrganizationApplicationErrors.EnrollmentTokenInvalid);
        }

        OrganizationEnrollmentLink? link = await organizations.GetEnrollmentLinkByDigestAsync(
            tokens.ComputeDigest(command.Token), cancellationToken).ConfigureAwait(false);
        if (link is null || !tokens.Verify(command.Token, link.TokenDigest))
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(OrganizationApplicationErrors.EnrollmentTokenInvalid);
        }

        Result<OrganizationSubjectId> subject = OrganizationSubjectId.Create(command.SubjectId);
        if (subject.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(subject.Error);
        }

        await governance.AcquireSharedAsync(
            link.OrganizationId,
            cancellationToken).ConfigureAwait(false);
        await joinSubjects.AcquireAsync(
            link.OrganizationId,
            subject.Value.Value,
            cancellationToken).ConfigureAwait(false);

        Organization? organization = await organizations.GetOrganizationAsync(
            link.OrganizationId, cancellationToken).ConfigureAwait(false);
        if (organization is not { Status: OrganizationState.Active })
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(
                organization is null ? OrganizationApplicationErrors.OrganizationNotFound :
                Gma.Modules.Organizations.Domain.Errors.OrganizationDomainErrors.OrganizationNotActive);
        }

        OrganizationEnrollmentClaim? existingClaim = await organizations.GetEnrollmentClaimBySubjectAsync(
            link.Id, subject.Value.Value, cancellationToken).ConfigureAwait(false);
        if (existingClaim is not null)
        {
            DateTimeOffset observedAtUtc = clock.UtcNow;
            if (existingClaim.IsDecisionDue(observedAtUtc))
            {
                return OrganizationEnrollmentClaimExpiry.Expire(
                    existingClaim, link, existingClaim.Version, observedAtUtc, ids);
            }

            return await CreateExistingOutcomeAsync(
                organization, existingClaim, subject.Value.Value, cancellationToken).ConfigureAwait(false);
        }

        OrganizationMembership? existingMembership = await organizations.GetMembershipAsync(
            organization.Id, subject.Value.Value, cancellationToken).ConfigureAwait(false);
        if (existingMembership is { Status: OrganizationMembershipState.Active })
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(OrganizationApplicationErrors.MembershipConflict);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        if (await organizations.HasCurrentPendingEnrollmentClaimAsync(
                organization.Id,
                subject.Value.Value,
                nowUtc,
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(
                OrganizationApplicationErrors.JoinRequestConflict);
        }

        Result productAdmission = await joinAdmissionPolicy.AuthorizeAsync(
            new OrganizationJoinAdmissionContext(
                OrganizationJoinAdmissionOperation.ClaimEnrollment,
                organization.Id,
                link.Id,
                null,
                subject.Value.Value,
                subject.Value.Value,
                OrganizationMappings.MapMode(link.ApprovalMode)),
            cancellationToken).ConfigureAwait(false);
        if (productAdmission.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(
                productAdmission.Error);
        }

        Result reserved = link.ReserveClaim(command.ActorId, ids.NewId(), nowUtc);
        if (reserved.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(reserved.Error);
        }

        if (link.ApprovalMode == DomainApprovalMode.RequiresApproval)
        {
            return await CreatePendingClaimAsync(
                link,
                subject.Value.Value,
                command.ActorId,
                nowUtc,
                cancellationToken).ConfigureAwait(false);
        }

        Result<OrganizationMembership> membership = await OrganizationMemberProvisioning.EnsureActiveMemberAsync(
            organizations, existingMembership, organization.Id, subject.Value.Value, command.ActorId,
            nowUtc, ids, cancellationToken).ConfigureAwait(false);
        if (membership.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(membership.Error);
        }

        Result<OrganizationEnrollmentClaim> claim = OrganizationEnrollmentClaim.Create(
            ids.NewId(), organization.Id, link.Id, subject.Value.Value,
            OrganizationEnrollmentClaimState.Accepted, membership.Value.Id,
            command.ActorId, ids.NewId(), nowUtc);
        if (claim.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(claim.Error);
        }

        await organizations.AddEnrollmentClaimAsync(claim.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(ToOutcome(claim.Value, organization, membership.Value));
    }

    private async Task<Result<OrganizationEnrollmentOutcomeDto>> CreatePendingClaimAsync(
        OrganizationEnrollmentLink link,
        string subjectId,
        string actorId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        Result<OrganizationEnrollmentClaim> claim = OrganizationEnrollmentClaim.Create(
            ids.NewId(), link.OrganizationId, link.Id, subjectId,
            OrganizationEnrollmentClaimState.Pending, null,
            actorId, ids.NewId(), nowUtc,
            nowUtc.AddHours(options.Value.EnrollmentClaimLifetimeHours));
        if (claim.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(claim.Error);
        }

        await organizations.AddEnrollmentClaimAsync(claim.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(new OrganizationEnrollmentOutcomeDto(claim.Value.ToDto(), null));
    }

    private async Task<Result<OrganizationEnrollmentOutcomeDto>> CreateExistingOutcomeAsync(
        Organization organization,
        OrganizationEnrollmentClaim claim,
        string subjectId,
        CancellationToken cancellationToken)
    {
        if (claim.Status is OrganizationEnrollmentClaimState.Rejected or
            OrganizationEnrollmentClaimState.Withdrawn)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(
                Gma.Modules.Organizations.Domain.Errors.OrganizationDomainErrors.EnrollmentClaimUnavailable);
        }

        if (claim.Status == OrganizationEnrollmentClaimState.Expired)
        {
            return Result.Success(new OrganizationEnrollmentOutcomeDto(claim.ToDto(), null));
        }

        if (claim.Status == OrganizationEnrollmentClaimState.Pending)
        {
            return Result.Success(new OrganizationEnrollmentOutcomeDto(claim.ToDto(), null));
        }

        OrganizationMembership? membership = await organizations.GetMembershipAsync(
            organization.Id, subjectId, cancellationToken).ConfigureAwait(false);
        return membership is { Status: OrganizationMembershipState.Active } && claim.MembershipId == membership.Id
            ? Result.Success(ToOutcome(claim, organization, membership))
            : Result.Failure<OrganizationEnrollmentOutcomeDto>(OrganizationApplicationErrors.MembershipConflict);
    }

    private static OrganizationEnrollmentOutcomeDto ToOutcome(
        OrganizationEnrollmentClaim claim,
        Organization organization,
        OrganizationMembership membership) => new(
        claim.ToDto(),
        new OrganizationMembershipSummaryDto(organization.ToDto(), membership.ToDto()));
}
