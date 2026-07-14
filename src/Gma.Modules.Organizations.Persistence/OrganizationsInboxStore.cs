namespace Gma.Modules.Organizations.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class OrganizationsInboxStore(OrganizationsDbContext dbContext, ISystemClock clock, IIdGenerator idGenerator)
    : EfInboxStore<OrganizationsDbContext>(dbContext, clock, idGenerator, OrganizationsMigrations.Schema);
