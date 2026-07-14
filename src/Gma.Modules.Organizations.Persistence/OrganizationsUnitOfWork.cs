namespace Gma.Modules.Organizations.Persistence;

using Gma.Framework.Application.Events;
using Gma.Framework.Persistence.EntityFrameworkCore;

internal sealed class OrganizationsUnitOfWork(OrganizationsDbContext dbContext, IDomainEventDispatcher domainEventDispatcher)
    : EfDomainEventUnitOfWork<OrganizationsDbContext>(OrganizationsMigrations.Schema, dbContext, domainEventDispatcher)
{
}
