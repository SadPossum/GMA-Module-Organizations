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
using DomainApprovalMode = Gma.Modules.Organizations.Domain.Enums.OrganizationEnrollmentApprovalMode;

internal sealed class ClaimOrganizationEnrollmentLinkCommandHandler(
    IOrganizationRepository organizations,
    IOrganizationEnrollmentTokenService tokens,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<ClaimOrganizationEnrollmentLinkCommand, OrganizationEnrollmentOutcomeDto>
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

        Organization? organization = await organizations.GetOrganizationAsync(
            link.OrganizationId, cancellationToken).ConfigureAwait(false);
        if (organization is not { Status: OrganizationState.Active })
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(
                organization is null ? OrganizationApplicationErrors.OrganizationNotFound :
                Gma.Modules.Organizations.Domain.Errors.OrganizationDomainErrors.OrganizationNotActive);
        }

        OrganizationEnrollmentClaim? existingClaim = await organizations.GetEnrollmentClaimBySubjectAsync(
            link.Id, command.SubjectId, cancellationToken).ConfigureAwait(false);
        if (existingClaim is not null)
        {
            return await CreateExistingOutcomeAsync(
                organization, existingClaim, command.SubjectId, cancellationToken).ConfigureAwait(false);
        }

        OrganizationMembership? existingMembership = await organizations.GetMembershipAsync(
            organization.Id, command.SubjectId, cancellationToken).ConfigureAwait(false);
        if (existingMembership is { Status: OrganizationMembershipState.Active })
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(OrganizationApplicationErrors.MembershipConflict);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result reserved = link.ReserveClaim(command.ActorId, ids.NewId(), nowUtc);
        if (reserved.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(reserved.Error);
        }

        if (link.ApprovalMode == DomainApprovalMode.RequiresApproval)
        {
            return await CreatePendingClaimAsync(link, command, nowUtc, cancellationToken).ConfigureAwait(false);
        }

        Result<OrganizationMembership> membership = await OrganizationMemberProvisioning.EnsureActiveMemberAsync(
            organizations, organization.Id, command.SubjectId, command.ActorId,
            nowUtc, ids, cancellationToken).ConfigureAwait(false);
        if (membership.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(membership.Error);
        }

        Result<OrganizationEnrollmentClaim> claim = OrganizationEnrollmentClaim.Create(
            ids.NewId(), organization.Id, link.Id, command.SubjectId,
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
        ClaimOrganizationEnrollmentLinkCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        Result<OrganizationEnrollmentClaim> claim = OrganizationEnrollmentClaim.Create(
            ids.NewId(), link.OrganizationId, link.Id, command.SubjectId,
            OrganizationEnrollmentClaimState.Pending, null,
            command.ActorId, ids.NewId(), nowUtc);
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
        if (claim.Status == OrganizationEnrollmentClaimState.Rejected)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(
                Gma.Modules.Organizations.Domain.Errors.OrganizationDomainErrors.EnrollmentClaimUnavailable);
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
