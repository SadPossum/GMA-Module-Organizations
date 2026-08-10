namespace Gma.Modules.Organizations.Persistence.Repositories;

using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

internal sealed class OrganizationCreationCoordinator(
    OrganizationsDbContext dbContext) : IOrganizationCreationCoordinator
{
    public async Task<OrganizationCreationAcquisition> AcquireAsync(
        Guid operationId,
        CancellationToken cancellationToken)
    {
        await OrganizationScopeExistenceTransactionLock.AcquireAsync(
            dbContext,
            operationId,
            cancellationToken).ConfigureAwait(false);
        Organization? organization = await dbContext.Organizations
            .SingleOrDefaultAsync(
            organization => organization.Id == operationId,
            cancellationToken).ConfigureAwait(false);
        bool isScopeClosed = await dbContext.OrganizationScopeStates
            .AsNoTracking()
            .AnyAsync(
                state => state.OrganizationId == operationId &&
                    state.IsClosed,
                cancellationToken).ConfigureAwait(false);
        return new OrganizationCreationAcquisition(
            organization,
            isScopeClosed);
    }
}
