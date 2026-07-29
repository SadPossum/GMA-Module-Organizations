namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Domain.Aggregates;

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
