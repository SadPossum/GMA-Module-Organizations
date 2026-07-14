namespace Gma.Modules.Organizations.Persistence;

using Microsoft.Extensions.Options;
using Gma.Framework.Messaging.Infrastructure;

internal sealed class OrganizationsOutboxStore(OrganizationsDbContext dbContext, IOptions<OutboxOptions> options)
    : EfOutboxStore<OrganizationsDbContext>(dbContext, options, OrganizationsMigrations.Schema);
