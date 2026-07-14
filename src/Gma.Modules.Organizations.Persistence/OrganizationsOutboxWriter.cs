namespace Gma.Modules.Organizations.Persistence;

using Microsoft.Extensions.Options;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Time;

internal sealed class OrganizationsOutboxWriter(
    OrganizationsDbContext dbContext,
    ISystemClock clock,
    IOptions<ApplicationIdentityOptions> applicationIdentity)
    : EfOutboxWriter<OrganizationsDbContext>(dbContext, clock, applicationIdentity, OrganizationsMigrations.Schema);
