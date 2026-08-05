namespace Gma.Modules.Organizations.Tests.Persistence;

using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationsOutboxWriterTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Scoped_event_persists_its_scope_coordinate()
    {
        await using OrganizationsDbContext dbContext = CreateDbContext();
        OrganizationsOutboxWriter writer = new(
            dbContext,
            new FixedClock(),
            Options.Create(new ApplicationIdentityOptions()),
            [new ScopedEventResolver()]);
        Guid organizationId = Guid.NewGuid();
        OrganizationChangedIntegrationEvent integrationEvent = new(
            Guid.NewGuid(),
            Now,
            organizationId.ToString("D"),
            organizationId,
            OrganizationChange.Created,
            OrganizationStatus.Active,
            organizationVersion: 1);

        await writer.EnqueueAsync(integrationEvent, CancellationToken.None);

        OutboxMessage message = Assert.Single(
            dbContext.ChangeTracker.Entries<OutboxMessage>()).Entity;
        Assert.Equal(integrationEvent.ScopeId, message.ScopeId);
    }

    private static OrganizationsDbContext CreateDbContext()
    {
        DbContextOptions<OrganizationsDbContext> options =
            new DbContextOptionsBuilder<OrganizationsDbContext>()
                .UseInMemoryDatabase($"organizations-{Guid.NewGuid():N}")
                .Options;
        return new(options);
    }

    private sealed class FixedClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class ScopedEventResolver
        : IIntegrationEventScopeResolver
    {
        public string? ResolveScopeId(IIntegrationEvent integrationEvent) =>
            (integrationEvent as IScopedIntegrationEvent)?.ScopeId;
    }
}
