namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Errors;

internal static class OrganizationLifecycleMaintenance
{
    public const string ActorId = "system:organizations-lifecycle";

    public static Result ValidateBatchSize(int batchSize) =>
        batchSize is >= 1 and <= 10_000
            ? Result.Success()
            : Result.Failure(OrganizationDomainErrors.EnrollmentConfigurationInvalid);
}

internal sealed class ExpireOrganizationInvitationsCommandHandler(
    IOrganizationLifecycleRepository lifecycle,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<ExpireOrganizationInvitationsCommand, int>
{
    public async Task<Result<int>> HandleAsync(
        ExpireOrganizationInvitationsCommand command,
        CancellationToken cancellationToken)
    {
        Result valid = OrganizationLifecycleMaintenance.ValidateBatchSize(command.BatchSize);
        if (valid.IsFailure)
        {
            return Result.Failure<int>(valid.Error);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        OrganizationInvitation[] invitations = await lifecycle.ListDueInvitationsAsync(
            nowUtc, command.BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (OrganizationInvitation invitation in invitations)
        {
            Result expired = invitation.Expire(
                invitation.Version,
                OrganizationLifecycleMaintenance.ActorId,
                ids.NewId(),
                nowUtc);
            if (expired.IsFailure)
            {
                return Result.Failure<int>(expired.Error);
            }
        }

        return Result.Success(invitations.Length);
    }
}

internal sealed class ExpireOrganizationEnrollmentClaimsCommandHandler(
    IOrganizationLifecycleRepository lifecycle,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<ExpireOrganizationEnrollmentClaimsCommand, int>
{
    public async Task<Result<int>> HandleAsync(
        ExpireOrganizationEnrollmentClaimsCommand command,
        CancellationToken cancellationToken)
    {
        Result valid = OrganizationLifecycleMaintenance.ValidateBatchSize(command.BatchSize);
        if (valid.IsFailure)
        {
            return Result.Failure<int>(valid.Error);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        OrganizationEnrollmentClaimExpiryCandidate[] candidates =
            await lifecycle.ListDueEnrollmentClaimsAsync(
                nowUtc, command.BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (OrganizationEnrollmentClaimExpiryCandidate candidate in candidates)
        {
            Result<OrganizationEnrollmentOutcomeDto> expired = OrganizationEnrollmentClaimExpiry.Expire(
                candidate.Claim, candidate.Link, candidate.Claim.Version, nowUtc, ids);
            if (expired.IsFailure)
            {
                return Result.Failure<int>(expired.Error);
            }
        }

        return Result.Success(candidates.Length);
    }
}

internal sealed class ExpireOrganizationEnrollmentLinksCommandHandler(
    IOrganizationLifecycleRepository lifecycle,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<ExpireOrganizationEnrollmentLinksCommand, int>
{
    public async Task<Result<int>> HandleAsync(
        ExpireOrganizationEnrollmentLinksCommand command,
        CancellationToken cancellationToken)
    {
        Result valid = OrganizationLifecycleMaintenance.ValidateBatchSize(command.BatchSize);
        if (valid.IsFailure)
        {
            return Result.Failure<int>(valid.Error);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        OrganizationEnrollmentLink[] links = await lifecycle.ListDueEnrollmentLinksAsync(
            nowUtc, command.BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (OrganizationEnrollmentLink link in links)
        {
            Result expired = link.Expire(
                link.Version,
                OrganizationLifecycleMaintenance.ActorId,
                ids.NewId(),
                nowUtc);
            if (expired.IsFailure)
            {
                return Result.Failure<int>(expired.Error);
            }
        }

        return Result.Success(links.Length);
    }
}
