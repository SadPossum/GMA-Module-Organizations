namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;

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
