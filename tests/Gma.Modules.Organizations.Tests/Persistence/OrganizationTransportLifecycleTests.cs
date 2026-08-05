namespace Gma.Modules.Organizations.Tests.Persistence;

using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Entities;
using Gma.Modules.Organizations.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationTransportLifecycleTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Outbox_claim_advances_open_scope_and_suppresses_closed_scope()
    {
        await using OrganizationsDbContext openContext = CreateDbContext();
        Guid openOrganizationId = Id(1);
        OutboxMessage openMessage = CreateOutbox(
            Id(2),
            openOrganizationId.ToString("D"));
        openContext.AddRange(
            CreateOrganization(openOrganizationId),
            openMessage);
        await openContext.SaveChangesAsync();
        OrganizationsOutboxStore openStore = new(
            openContext,
            Options.Create(new OutboxOptions()));

        IReadOnlyList<OutboxMessageRecord> claimed = await openStore
            .ClaimPendingAsync(
                batchSize: 10,
                "worker-a",
                Now,
                TimeSpan.FromMinutes(1),
                CancellationToken.None);

        Assert.Single(claimed);
        Assert.Equal(
            2,
            (await openContext.OrganizationScopeStates.SingleAsync()).Version);

        await using OrganizationsDbContext closedContext = CreateDbContext();
        Guid closedOrganizationId = Id(10);
        OutboxMessage closedMessage = CreateOutbox(
            Id(11),
            closedOrganizationId.ToString("D"));
        closedContext.AddRange(
            CreateOrganization(closedOrganizationId),
            closedMessage);
        await closedContext.SaveChangesAsync();
        OrganizationScopeState state = await closedContext
            .OrganizationScopeStates.SingleAsync();
        Assert.Equal(
            OrganizationScopeCloseTransition.Completed,
            state.Close(Id(12), new string('a', 64), Now));
        await closedContext.SaveChangesAsync();
        OrganizationsOutboxStore closedStore = new(
            closedContext,
            Options.Create(new OutboxOptions()));

        IReadOnlyList<OutboxMessageRecord> suppressed = await closedStore
            .ClaimPendingAsync(
                batchSize: 10,
                "worker-a",
                Now,
                TimeSpan.FromMinutes(1),
                CancellationToken.None);

        Assert.Empty(suppressed);
        Assert.Null(closedMessage.LockedBy);
        Assert.Equal(2, state.Version);
    }

    [Fact]
    public async Task Inbox_suppresses_closed_and_malformed_scoped_messages()
    {
        await using OrganizationsDbContext dbContext = CreateDbContext();
        Guid organizationId = Id(20);
        dbContext.Organizations.Add(CreateOrganization(organizationId));
        await dbContext.SaveChangesAsync();
        OrganizationScopeState state = await dbContext
            .OrganizationScopeStates.SingleAsync();
        Assert.Equal(
            OrganizationScopeCloseTransition.Completed,
            state.Close(Id(21), new string('b', 64), Now));
        await dbContext.SaveChangesAsync();
        OrganizationsInboxStore store = new(
            dbContext,
            new FixedClock(),
            new FixedIds());
        bool invoked = false;

        InboxProcessResult closed = await store.ProcessAsync(
            Message(Id(22), organizationId.ToString("D")),
            _ =>
            {
                invoked = true;
                return Task.CompletedTask;
            },
            CancellationToken.None);
        InboxProcessResult malformed = await store.ProcessAsync(
            Message(Id(23), "not-an-organization-id"),
            _ =>
            {
                invoked = true;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(InboxProcessStatus.Suppressed, closed.Status);
        Assert.Equal(InboxProcessStatus.Suppressed, malformed.Status);
        Assert.False(invoked);
        Assert.Empty(await dbContext.InboxMessages.ToArrayAsync());
        Assert.Equal(2, state.Version);
    }

    private static InboxMessageRecord Message(Guid id, string scopeId) =>
        new(
            id,
            "organization-test-handler",
            "gma.organizations.test.v1",
            "organization-test",
            version: 1,
            scopeId,
            Now);

    private static Organization CreateOrganization(Guid organizationId) =>
        Organization.Create(
            organizationId,
            "Transport House",
            $"transport-house-{organizationId:N}",
            "user:owner",
            Guid.NewGuid(),
            Now).Value;

    private static OutboxMessage CreateOutbox(Guid id, string scopeId) =>
        new(
            id,
            "gma.organizations.test.v1",
            "organization-test",
            version: 1,
            scopeId,
            Now,
            "{}",
            Now);

    private static OrganizationsDbContext CreateDbContext()
    {
        DbContextOptions<OrganizationsDbContext> options =
            new DbContextOptionsBuilder<OrganizationsDbContext>()
                .UseInMemoryDatabase(
                    $"organization-transport-{Guid.NewGuid():N}",
                    new InMemoryDatabaseRoot())
                .ConfigureWarnings(warnings => warnings.Ignore(
                    InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        return new OrganizationsDbContext(options);
    }

    private static Guid Id(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");

    private sealed class FixedClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class FixedIds : IIdGenerator
    {
        public Guid NewId() => Id(99);
    }
}
