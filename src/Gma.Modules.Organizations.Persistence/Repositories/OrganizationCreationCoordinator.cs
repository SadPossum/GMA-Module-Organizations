namespace Gma.Modules.Organizations.Persistence.Repositories;

using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

internal sealed class OrganizationCreationCoordinator(
    OrganizationsDbContext dbContext) : IOrganizationCreationCoordinator
{
    public async Task<Organization?> AcquireAsync(
        Guid operationId,
        CancellationToken cancellationToken)
    {
        await EfTransactionKeyLock.AcquireAsync(
            dbContext,
            $"gma:organizations:create:{operationId:N}",
            cancellationToken).ConfigureAwait(false);
        return await dbContext.Organizations.SingleOrDefaultAsync(
            organization => organization.Id == operationId,
            cancellationToken).ConfigureAwait(false);
    }
}
