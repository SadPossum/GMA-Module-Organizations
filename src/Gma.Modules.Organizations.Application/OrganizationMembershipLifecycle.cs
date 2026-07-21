namespace Gma.Modules.Organizations.Application;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Contracts;

internal sealed class OrganizationMembershipLifecycle(IRequestDispatcher dispatcher)
    : IOrganizationMembershipLifecycle
{
    public async Task<OrganizationMembershipLifecycleResult> EnsureStateAsync(
        Guid organizationId,
        string subjectId,
        OrganizationMembershipStatus desiredStatus,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("An organization id is required.", nameof(organizationId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        if (desiredStatus == OrganizationMembershipStatus.Unknown || !Enum.IsDefined(desiredStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(desiredStatus));
        }

        Result<OrganizationMembershipLifecycleResult> result = await dispatcher.SendAsync(
                new EnsureOrganizationMembershipStateCommand(
                    organizationId,
                    subjectId,
                    desiredStatus,
                    actorId),
                cancellationToken)
            .ConfigureAwait(false);
        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException(result.Error.Message);
    }
}
