namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Domain.Aggregates;

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
