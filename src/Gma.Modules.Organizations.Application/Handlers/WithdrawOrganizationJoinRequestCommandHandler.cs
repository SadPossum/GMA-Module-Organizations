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
using Gma.Modules.Organizations.Domain.ValueObjects;

internal sealed class WithdrawOrganizationJoinRequestCommandHandler(
    IOrganizationRepository organizations,
    IOrganizationGovernanceCoordinator governance,
    IOrganizationJoinSubjectCoordinator joinSubjects,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<WithdrawOrganizationJoinRequestCommand, OrganizationEnrollmentOutcomeDto>
{
    public async Task<Result<OrganizationEnrollmentOutcomeDto>> HandleAsync(
        WithdrawOrganizationJoinRequestCommand command,
        CancellationToken cancellationToken)
    {
        Result<OrganizationSubjectId> subject = OrganizationSubjectId.Create(command.SubjectId);
        if (subject.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(subject.Error);
        }

        await governance.AcquireSharedAsync(
            command.OrganizationId,
            cancellationToken).ConfigureAwait(false);
        await joinSubjects.AcquireAsync(
            command.OrganizationId,
            subject.Value.Value,
            cancellationToken).ConfigureAwait(false);

        OrganizationEnrollmentClaim? claim = await organizations.GetEnrollmentClaimAsync(
            command.OrganizationId,
            command.ClaimId,
            cancellationToken).ConfigureAwait(false);
        if (claim is null || !string.Equals(
                claim.SubjectId,
                subject.Value.Value,
                StringComparison.Ordinal))
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(
                OrganizationApplicationErrors.EnrollmentClaimNotFound);
        }

        if (IsExactTerminalReplay(command, claim))
        {
            return Result.Success(new OrganizationEnrollmentOutcomeDto(claim.ToDto(), null));
        }

        OrganizationEnrollmentLink? link = await organizations.GetEnrollmentLinkAsync(
            command.OrganizationId,
            claim.EnrollmentLinkId,
            cancellationToken).ConfigureAwait(false);
        if (link is null)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(
                OrganizationApplicationErrors.EnrollmentLinkNotFound);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        if (claim.IsDecisionDue(nowUtc))
        {
            return OrganizationEnrollmentClaimExpiry.Expire(
                claim,
                link,
                command.ExpectedClaimVersion,
                nowUtc,
                ids);
        }

        Result withdrawn = claim.Withdraw(
            command.ExpectedClaimVersion,
            command.ActorId,
            ids.NewId(),
            nowUtc);
        if (withdrawn.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(withdrawn.Error);
        }

        if (link.Status == OrganizationEnrollmentLinkState.Active && link.ReservedClaims > 0)
        {
            Result released = link.ReleaseClaim(
                link.Version,
                command.ActorId,
                ids.NewId(),
                nowUtc);
            if (released.IsFailure)
            {
                return Result.Failure<OrganizationEnrollmentOutcomeDto>(released.Error);
            }
        }

        return Result.Success(new OrganizationEnrollmentOutcomeDto(claim.ToDto(), null));
    }

    private static bool IsExactTerminalReplay(
        WithdrawOrganizationJoinRequestCommand command,
        OrganizationEnrollmentClaim claim) =>
        claim.Version > 1 &&
        command.ExpectedClaimVersion == claim.Version - 1 &&
        claim.Status is OrganizationEnrollmentClaimState.Withdrawn or
            OrganizationEnrollmentClaimState.Expired;
}
