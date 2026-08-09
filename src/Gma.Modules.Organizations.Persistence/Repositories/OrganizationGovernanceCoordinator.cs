namespace Gma.Modules.Organizations.Persistence.Repositories;

using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Modules.Organizations.Application.Ports;

internal sealed class OrganizationGovernanceCoordinator(
    OrganizationsDbContext dbContext) : IOrganizationGovernanceCoordinator
{
    public Task AcquireSharedAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        this.AcquireAsync(
            organizationId,
            EfTransactionKeyLockMode.Shared,
            cancellationToken);

    public Task AcquireExclusiveAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        this.AcquireAsync(
            organizationId,
            EfTransactionKeyLockMode.Exclusive,
            cancellationToken);

    private Task AcquireAsync(
        Guid organizationId,
        EfTransactionKeyLockMode mode,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Organization id is required for governance coordination.",
                nameof(organizationId));
        }

        return EfTransactionKeyLock.AcquireAsync(
            dbContext,
            $"gma:organizations:governance:{organizationId:N}",
            mode,
            cancellationToken);
    }
}
