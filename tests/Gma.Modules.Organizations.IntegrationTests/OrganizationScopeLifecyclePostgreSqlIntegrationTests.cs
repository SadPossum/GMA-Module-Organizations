namespace Gma.Modules.Organizations.IntegrationTests;

using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.IntegrationTests.Support;
using Gma.Modules.Organizations.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;
using DomainEnrollmentApprovalMode =
    Gma.Modules.Organizations.Domain.Enums.OrganizationEnrollmentApprovalMode;
using DomainMembershipRole =
    Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;

[Trait("Category", "Docker")]
[Trait("Category", "Integration")]
public sealed class OrganizationScopeLifecyclePostgreSqlIntegrationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 14, 0, 0, TimeSpan.Zero);

    [DockerFact]
    public async Task Scope_lifecycle_is_revision_fenced_suppressed_resumable_and_exactly_replayable()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("organization_scope_lifecycle_tests")
                .Build();
        await postgreSql.StartAsync();
        string connectionString = postgreSql.GetConnectionString();
        SeededScope seeded = await SeedAsync(connectionString);

        await VerifyJournalCleanupAsync(connectionString, seeded.OrganizationId);
        await VerifyRetentionAsync(connectionString, seeded);

        OrganizationScopeSnapshot selected = await SnapshotAsync(
            connectionString,
            seeded.OrganizationId);
        Assert.Equal(OrganizationScopeStatus.Open, selected.Status);
        Assert.Equal(4, selected.Revision);
        await VerifyExportAsync(connectionString, seeded, selected.Revision);

        await VerifyIndependentWriteConcurrencyAsync(connectionString, seeded);
        await using (OrganizationsDbContext staleContext =
                     CreateDbContext(connectionString))
        {
            OrganizationScopeLifecycleService lifecycle =
                new(staleContext, new FixedClock());
            OrganizationScopeExportPage stale = await lifecycle.ExportAsync(
                new OrganizationScopeExportRequest(
                    seeded.OrganizationId,
                    selected.Revision,
                    OrganizationScopeExportStore.Organization,
                    AfterCursor: null,
                    PageSize: 10),
                CancellationToken.None);
            Assert.Equal(OrganizationScopeExportStatus.Stale, stale.Status);
        }

        await ClaimPendingOutboxAsync(connectionString, seeded);
        OrganizationScopeSnapshot leased = await SnapshotAsync(
            connectionString,
            seeded.OrganizationId);
        await using (OrganizationsDbContext busyContext =
                     CreateDbContext(connectionString))
        {
            OrganizationScopeLifecycleService lifecycle =
                new(busyContext, new FixedClock());
            OrganizationScopeDestroyResult busy = await lifecycle
                .DestroyBatchAsync(
                    new OrganizationScopeDestroyRequest(
                        Id(80),
                        seeded.OrganizationId,
                        leased.Revision,
                        BatchSize: 1),
                    CancellationToken.None);
            Assert.Equal(OrganizationScopeDestroyStatus.Busy, busy.Status);
        }

        await ReleaseOutboxLeaseAsync(connectionString, seeded);
        OrganizationScopeSnapshot ready = await SnapshotAsync(
            connectionString,
            seeded.OrganizationId);
        OrganizationScopeDestroyRequest request = new(
            Id(81),
            seeded.OrganizationId,
            ready.Revision,
            BatchSize: 1);
        OrganizationScopeDestroyResult first = await DestroyOnceAsync(
            connectionString,
            request);
        Assert.Equal(OrganizationScopeDestroyStatus.InProgress, first.Status);
        Assert.Equal(
            OrganizationScopeDestructionStage.InboxMessages,
            first.Progress!.Stage);
        Assert.Equal(1, first.Progress.RemovedRecordCount);

        await VerifyClosedScopeSuppressionAsync(connectionString, seeded);

        OrganizationScopeDestroyResult result = first;
        int calls = 1;
        while (result.Status == OrganizationScopeDestroyStatus.InProgress)
        {
            result = await DestroyOnceAsync(connectionString, request);
            calls++;
            Assert.InRange(calls, 2, 10);
        }

        Assert.Equal(OrganizationScopeDestroyStatus.Completed, result.Status);
        Assert.Equal(9, result.Receipt!.RemovedRecordCount);
        Assert.Equal(9, result.Receipt.CompletedBatchCount);
        OrganizationScopeDestroyResult replay = await DestroyOnceAsync(
            connectionString,
            request);
        Assert.Equal(OrganizationScopeDestroyStatus.Replayed, replay.Status);
        Assert.Equal(result.Receipt, replay.Receipt);

        await VerifyTerminalStateAsync(
            connectionString,
            seeded.OrganizationId,
            request.OperationId);
        await VerifyTerminalProtectionAsync(
            connectionString,
            seeded.OrganizationId);
    }

    private static async Task<SeededScope> SeedAsync(string connectionString)
    {
        await using OrganizationsDbContext dbContext =
            CreateDbContext(connectionString);
        await dbContext.Database.MigrateAsync();
        Guid organizationId = Id(1);
        Organization organization = Organization.Create(
            organizationId,
            "Lifecycle House",
            "lifecycle-house",
            "user:owner",
            Id(2),
            Now.AddDays(-30)).Value;
        OrganizationMembership owner = CreateMembership(
            Id(3),
            organizationId,
            "owner",
            DomainMembershipRole.Owner);
        OrganizationMembership member = CreateMembership(
            Id(4),
            organizationId,
            "member",
            DomainMembershipRole.Member);
        OrganizationInvitation invitation = OrganizationInvitation.Create(
            Id(5),
            organizationId,
            "owner",
            "active@example.test",
            new string('a', 64),
            Now.AddDays(7),
            "user:owner",
            Id(6),
            Now).Value;
        OrganizationInvitation retainedInvitation =
            OrganizationInvitation.Create(
                Id(7),
                organizationId,
                "owner",
                "expired@example.test",
                new string('b', 64),
                Now.AddDays(-20),
                "user:owner",
                Id(8),
                Now.AddDays(-30)).Value;
        Assert.True(retainedInvitation.Expire(
            retainedInvitation.Version,
            "system:lifecycle",
            Id(9),
            Now.AddDays(-20)).IsSuccess);
        OrganizationEnrollmentLink link = OrganizationEnrollmentLink.Create(
            Id(10),
            organizationId,
            "owner",
            new string('c', 64),
            Now.AddDays(7),
            maximumClaims: 10,
            DomainEnrollmentApprovalMode.RequiresApproval,
            "user:owner",
            Id(11),
            Now).Value;
        Assert.True(link.ReserveClaim(
            "user:claimant",
            Id(12),
            Now.AddMinutes(1)).IsSuccess);
        OrganizationEnrollmentClaim claim =
            OrganizationEnrollmentClaim.Create(
                Id(13),
                organizationId,
                link.Id,
                "claimant",
                OrganizationEnrollmentClaimState.Pending,
                membershipId: null,
                "user:claimant",
                Id(14),
                Now.AddMinutes(1),
                Now.AddDays(2)).Value;
        string scopeId = organizationId.ToString("D");
        OutboxMessage processedOutbox = CreateOutbox(
            Id(20),
            scopeId,
            Now.AddDays(-2));
        processedOutbox.MarkClaimed(
            "worker-old",
            Now.AddDays(-2),
            TimeSpan.FromMinutes(5));
        processedOutbox.MarkProcessed(Now.AddDays(-2).AddMinutes(1));
        OutboxMessage pendingOutbox = CreateOutbox(Id(21), scopeId, Now);
        InboxMessage processedInbox = CreateInbox(
            Id(30),
            "processed-handler",
            scopeId,
            Now.AddDays(-2));
        processedInbox.MarkProcessing("worker-old", Now.AddDays(-2));
        processedInbox.MarkProcessed(Now.AddDays(-2).AddMinutes(1));
        InboxMessage firstPendingInbox = CreateInbox(
            Id(31),
            "handler-a",
            scopeId,
            Now);
        InboxMessage secondPendingInbox = CreateInbox(
            Id(31),
            "handler-b",
            scopeId,
            Now);
        dbContext.AddRange(
            organization,
            owner,
            member,
            invitation,
            retainedInvitation,
            link,
            claim,
            processedOutbox,
            pendingOutbox,
            processedInbox,
            firstPendingInbox,
            secondPendingInbox);
        await dbContext.SaveChangesAsync();
        Assert.Equal(
            1,
            (await dbContext.OrganizationScopeStates.SingleAsync()).Version);
        return new SeededScope(
            organizationId,
            owner.Id,
            invitation.Id,
            retainedInvitation.Id,
            pendingOutbox.Id);
    }

    private static async Task VerifyJournalCleanupAsync(
        string connectionString,
        Guid organizationId)
    {
        await using OrganizationsDbContext dbContext =
            CreateDbContext(connectionString);
        OrganizationsOutboxStore outbox = new(
            dbContext,
            Options.Create(new OutboxOptions()));
        OrganizationsInboxStore inbox = new(
            dbContext,
            new FixedClock(),
            new FixedIds());

        Assert.Equal(1, await outbox.DeleteProcessedBeforeAsync(
            Now.AddDays(-1),
            maxMessages: 10,
            CancellationToken.None));
        Assert.Equal(1, await inbox.DeleteProcessedBeforeAsync(
            Now.AddDays(-1),
            maxMessages: 10,
            CancellationToken.None));
        Assert.Equal(
            3,
            (await dbContext.OrganizationScopeStates.SingleAsync(state =>
                state.OrganizationId == organizationId)).Version);
    }

    private static async Task VerifyRetentionAsync(
        string connectionString,
        SeededScope seeded)
    {
        ServiceCollection services = new();
        services.AddScoped(_ => CreateDbContext(connectionString));
        await using ServiceProvider provider = services.BuildServiceProvider();
        OrganizationsRetentionService retention = new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FixedClock(),
            Options.Create(new OrganizationsRetentionOptions
            {
                Enabled = true,
                InvitationHistoryDays = 7,
                EnrollmentHistoryDays = 7,
                BatchSize = 10,
                MaxBatchesPerCategoryPerCycle = 1,
                IntervalMinutes = 60
            }),
            NullLogger<OrganizationsRetentionService>.Instance);

        await retention.CleanupAsync(CancellationToken.None);

        await using OrganizationsDbContext verification =
            CreateDbContext(connectionString);
        Assert.False(await verification.Invitations.AnyAsync(invitation =>
            invitation.Id == seeded.RetainedInvitationId));
        Assert.Equal(
            4,
            (await verification.OrganizationScopeStates.SingleAsync()).Version);
    }

    private static async Task VerifyExportAsync(
        string connectionString,
        SeededScope seeded,
        long revision)
    {
        await using OrganizationsDbContext dbContext =
            CreateDbContext(connectionString);
        OrganizationScopeLifecycleService lifecycle =
            new(dbContext, new FixedClock());
        foreach (OrganizationScopeExportStore store in Enum
                     .GetValues<OrganizationScopeExportStore>()
                     .Where(store => store != OrganizationScopeExportStore.Unknown))
        {
            OrganizationScopeExportPage page = await lifecycle.ExportAsync(
                new OrganizationScopeExportRequest(
                    seeded.OrganizationId,
                    revision,
                    store,
                    AfterCursor: null,
                    PageSize: 50),
                CancellationToken.None);
            Assert.Equal(OrganizationScopeExportStatus.Completed, page.Status);
            Assert.NotEmpty(page.Records);
            Assert.All(page.Records, record => Assert.DoesNotContain(
                record.GetType().GetProperties(),
                property => property.Name.Contains(
                    "Digest",
                    StringComparison.OrdinalIgnoreCase)));
        }
    }

    private static async Task VerifyIndependentWriteConcurrencyAsync(
        string connectionString,
        SeededScope seeded)
    {
        await using OrganizationsDbContext first =
            CreateDbContext(connectionString);
        await using OrganizationsDbContext second =
            CreateDbContext(connectionString);
        _ = await first.OrganizationScopeStates.SingleAsync();
        _ = await second.OrganizationScopeStates.SingleAsync();
        OrganizationMembership membership = await first.Memberships
            .SingleAsync(candidate => candidate.Id == seeded.OwnerMembershipId);
        OrganizationInvitation invitation = await second.Invitations
            .SingleAsync(candidate => candidate.Id == seeded.InvitationId);
        Assert.True(membership.Suspend(
            membership.Version,
            "user:owner",
            Id(40),
            Now.AddMinutes(2)).IsSuccess);
        Assert.True(invitation.Revoke(
            invitation.Version,
            "user:owner",
            Id(41),
            Now.AddMinutes(2)).IsSuccess);

        Exception?[] results = await Task.WhenAll(
            CaptureAsync(() => first.SaveChangesAsync()),
            CaptureAsync(() => second.SaveChangesAsync()));

        Assert.Single(results, exception => exception is null);
        Assert.Single(results, exception =>
            exception is DbUpdateConcurrencyException);
    }

    private static async Task ClaimPendingOutboxAsync(
        string connectionString,
        SeededScope seeded)
    {
        await using OrganizationsDbContext dbContext =
            CreateDbContext(connectionString);
        OrganizationsOutboxStore store = new(
            dbContext,
            Options.Create(new OutboxOptions()));
        IReadOnlyList<OutboxMessageRecord> claimed = await store
            .ClaimPendingAsync(
                batchSize: 10,
                "worker-active",
                Now,
                TimeSpan.FromMinutes(5),
                CancellationToken.None);
        Assert.Equal(seeded.PendingOutboxId, Assert.Single(claimed).Id);
    }

    private static async Task ReleaseOutboxLeaseAsync(
        string connectionString,
        SeededScope seeded)
    {
        await using OrganizationsDbContext dbContext =
            CreateDbContext(connectionString);
        OutboxMessage outbox = await dbContext.OutboxMessages.SingleAsync(
            message => message.Id == seeded.PendingOutboxId);
        outbox.MarkFailed(
            "retry later",
            Now.AddMinutes(1),
            maxAttempts: 10);
        await dbContext.SaveChangesAsync();
    }

    private static async Task VerifyClosedScopeSuppressionAsync(
        string connectionString,
        SeededScope seeded)
    {
        await using (OrganizationsDbContext inboxContext =
                     CreateDbContext(connectionString))
        {
            OrganizationsInboxStore inbox = new(
                inboxContext,
                new FixedClock(),
                new FixedIds());
            bool invoked = false;
            InboxProcessResult result = await inbox.ProcessAsync(
                new InboxMessageRecord(
                    Id(82),
                    "late-handler",
                    "gma.organizations.test.v1",
                    "organization-test",
                    version: 1,
                    seeded.OrganizationId.ToString("D"),
                    Now),
                _ =>
                {
                    invoked = true;
                    return Task.CompletedTask;
                },
                CancellationToken.None);
            Assert.Equal(InboxProcessStatus.Suppressed, result.Status);
            Assert.False(invoked);
        }

        await using (OrganizationsDbContext outboxContext =
                     CreateDbContext(connectionString))
        {
            OrganizationsOutboxStore outbox = new(
                outboxContext,
                Options.Create(new OutboxOptions()));
            Assert.Empty(await outbox.ClaimPendingAsync(
                batchSize: 10,
                "worker-late",
                Now.AddMinutes(2),
                TimeSpan.FromMinutes(1),
                CancellationToken.None));
        }

        await using (OrganizationsDbContext writeContext =
                     CreateDbContext(connectionString))
        {
            writeContext.Memberships.Add(CreateMembership(
                Id(83),
                seeded.OrganizationId,
                "late-subject",
                DomainMembershipRole.Member));
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
                writeContext.SaveChangesAsync());
        }
    }

    private static async Task<OrganizationScopeSnapshot> SnapshotAsync(
        string connectionString,
        Guid organizationId)
    {
        await using OrganizationsDbContext dbContext =
            CreateDbContext(connectionString);
        return await new OrganizationScopeLifecycleService(
                dbContext,
                new FixedClock())
            .GetSnapshotAsync(organizationId, CancellationToken.None);
    }

    private static async Task<OrganizationScopeDestroyResult> DestroyOnceAsync(
        string connectionString,
        OrganizationScopeDestroyRequest request)
    {
        await using OrganizationsDbContext dbContext =
            CreateDbContext(connectionString);
        return await new OrganizationScopeLifecycleService(
                dbContext,
                new FixedClock())
            .DestroyBatchAsync(request, CancellationToken.None);
    }

    private static async Task VerifyTerminalStateAsync(
        string connectionString,
        Guid organizationId,
        Guid operationId)
    {
        await using OrganizationsDbContext dbContext =
            CreateDbContext(connectionString);
        Assert.Empty(await dbContext.Organizations.ToArrayAsync());
        Assert.Empty(await dbContext.Memberships.ToArrayAsync());
        Assert.Empty(await dbContext.Invitations.ToArrayAsync());
        Assert.Empty(await dbContext.EnrollmentLinks.ToArrayAsync());
        Assert.Empty(await dbContext.EnrollmentClaims.ToArrayAsync());
        Assert.Empty(await dbContext.InboxMessages.ToArrayAsync());
        Assert.Empty(await dbContext.OutboxMessages.ToArrayAsync());
        Assert.Empty(await dbContext.OrganizationScopeDestroyOperations
            .ToArrayAsync());
        Assert.Equal(
            organizationId,
            (await dbContext.OrganizationScopeStates.SingleAsync())
                .OrganizationId);
        Assert.Equal(
            operationId,
            (await dbContext.OrganizationScopeDestroyReceipts.SingleAsync())
                .OperationId);
    }

    private static async Task VerifyTerminalProtectionAsync(
        string connectionString,
        Guid organizationId)
    {
        await using (OrganizationsDbContext stateMutation =
                     CreateDbContext(connectionString))
        {
            PostgresException failure = await Assert.ThrowsAsync<PostgresException>(
                () => stateMutation.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE organizations.organization_scope_states
                    SET "IsClosed" = FALSE
                    WHERE "OrganizationId" = {organizationId};
                    """));
            Assert.Equal("P0001", failure.SqlState);
            Assert.Contains("closed organization scope", failure.MessageText);
        }

        await using (OrganizationsDbContext receiptMutation =
                     CreateDbContext(connectionString))
        {
            PostgresException failure = await Assert.ThrowsAsync<PostgresException>(
                () => receiptMutation.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE organizations.organization_scope_destroy_receipts
                    SET "RemovedRecordCount" = "RemovedRecordCount" + 1
                    WHERE "OrganizationId" = {organizationId};
                    """));
            Assert.Equal("P0001", failure.SqlState);
            Assert.Contains("append-only", failure.MessageText);
        }
    }

    private static OrganizationMembership CreateMembership(
        Guid id,
        Guid organizationId,
        string subjectId,
        DomainMembershipRole role) =>
        OrganizationMembership.Create(
            id,
            organizationId,
            subjectId,
            role,
            "user:owner",
            Guid.NewGuid(),
            Now).Value;

    private static OutboxMessage CreateOutbox(
        Guid id,
        string scopeId,
        DateTimeOffset createdAtUtc) =>
        new(
            id,
            "gma.organizations.test.v1",
            "organization-test",
            version: 1,
            scopeId,
            createdAtUtc,
            "{}",
            createdAtUtc);

    private static InboxMessage CreateInbox(
        Guid id,
        string handler,
        string scopeId,
        DateTimeOffset createdAtUtc) =>
        InboxMessage.Create(
            id,
            handler,
            "gma.organizations.test.v1",
            "organization-test",
            version: 1,
            scopeId,
            createdAtUtc,
            createdAtUtc);

    private static OrganizationsDbContext CreateDbContext(
        string connectionString) =>
        new(new DbContextOptionsBuilder<OrganizationsDbContext>()
            .UseNpgsql(connectionString, provider => provider
                .MigrationsAssembly(
                    OrganizationsMigrations.PostgreSqlAssembly)
                .MigrationsHistoryTable(
                    OrganizationsMigrations.HistoryTable,
                    OrganizationsMigrations.Schema))
            .Options);

    private static async Task<Exception?> CaptureAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static Guid Id(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");

    private sealed record SeededScope(
        Guid OrganizationId,
        Guid OwnerMembershipId,
        Guid InvitationId,
        Guid RetainedInvitationId,
        Guid PendingOutboxId);

    private sealed class FixedClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class FixedIds : IIdGenerator
    {
        public Guid NewId() => Id(99);
    }
}
