namespace Gma.Modules.Organizations.IntegrationTests;

using System.Data;
using Gma.Framework.Application.Events.Infrastructure;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.IntegrationTests.Support;
using Gma.Modules.Organizations.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

[Trait("Category", "Docker")]
[Trait("Category", "Integration")]
public sealed class OrganizationProvisioningScopeLifecyclePostgreSqlIntegrationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    [DockerFact]
    public async Task Exact_replay_and_scope_destruction_linearize_per_organization()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("organization_provisioning_lifecycle_tests")
                .Build();
        await postgreSql.StartAsync();
        string connectionString = postgreSql.GetConnectionString();
        await using ServiceProvider provider = CreateProvider(connectionString);
        await MigrateAsync(provider);

        OrganizationProvisioningRequest replayFirst = Request(
            Guid.NewGuid(),
            "Replay First House",
            "replay-first-house",
            "owner-replay-first");
        OrganizationProvisioningRequest destroyFirst = Request(
            Guid.NewGuid(),
            "Destroy First House",
            "destroy-first-house",
            "owner-destroy-first");
        OrganizationProvisioningRequest unrelated = Request(
            Guid.NewGuid(),
            "Unrelated House",
            "unrelated-house",
            "owner-unrelated");
        OrganizationProvisioningRequest freshCreation = Request(
            Guid.NewGuid(),
            "Fresh Creation House",
            "fresh-creation-house",
            "owner-fresh-creation");

        await AssertProvisionedAsync(provider, replayFirst);
        await AssertProvisionedAsync(provider, destroyFirst);
        await AssertProvisionedAsync(provider, unrelated);

        await VerifyReplayFirstAsync(connectionString, replayFirst);
        await VerifyDestroyFirstAsync(
            connectionString,
            provider,
            destroyFirst,
            unrelated);
        await VerifyFreshCreationFirstAsync(connectionString, freshCreation);
    }

    private static async Task VerifyReplayFirstAsync(
        string connectionString,
        OrganizationProvisioningRequest request)
    {
        long revision = await OpenRevisionAsync(connectionString, request.OrganizationId);
        string replayApplication = ApplicationName("replay-holder");
        string destroyApplication = ApplicationName("destroy-waiter");
        await using ServiceProvider replayProvider = CreateProvider(
            WithApplicationName(connectionString, replayApplication));
        await using AsyncServiceScope replayScope = replayProvider.CreateAsyncScope();
        OrganizationsDbContext replayDbContext = replayScope.ServiceProvider
            .GetRequiredService<OrganizationsDbContext>();
        await using var replayTransaction = await replayDbContext.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted);
        int replayProcessId = BackendProcessId(replayDbContext);
        bool replayCommitted = false;
        try
        {
            IOrganizationProvisioner provisioner = replayScope.ServiceProvider
                .GetRequiredService<IOrganizationProvisioner>();
            OrganizationProvisioningResult replay = await provisioner.ProvisionAsync(
                request with { ActorId = "admin:replay-first-recovery" },
                CancellationToken.None);
            Assert.Equal(
                OrganizationProvisioningOutcome.AlreadyProvisioned,
                replay.Outcome);
            Assert.True(replay.IsSuccess);

            OrganizationScopeDestroyRequest destroyRequest = new(
                Guid.NewGuid(),
                request.OrganizationId,
                revision,
                BatchSize: 1);
            Task<OrganizationScopeDestroyResult> destroyTask = DestroyAsync(
                WithApplicationName(connectionString, destroyApplication),
                destroyRequest);
            await WaitForAdvisoryBlockAsync(
                connectionString,
                destroyApplication,
                replayProcessId,
                destroyTask);

            await replayTransaction.CommitAsync();
            replayCommitted = true;
            OrganizationScopeDestroyResult destroyed = await destroyTask
                .WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(OrganizationScopeDestroyStatus.InProgress, destroyed.Status);
            Assert.Equal(revision + 1, destroyed.Progress!.ResultingRevision);
            await AssertClosedLiveScopeAsync(
                connectionString,
                request.OrganizationId,
                destroyed.Progress.ResultingRevision);
        }
        finally
        {
            if (!replayCommitted)
            {
                await replayTransaction.RollbackAsync(CancellationToken.None);
            }
        }
    }

    private static async Task VerifyDestroyFirstAsync(
        string connectionString,
        ServiceProvider provider,
        OrganizationProvisioningRequest request,
        OrganizationProvisioningRequest unrelatedRequest)
    {
        long revision = await OpenRevisionAsync(connectionString, request.OrganizationId);
        string destroyApplication = ApplicationName("destroy-holder");
        string replayApplication = ApplicationName("replay-waiter");
        await using ServiceProvider destroyProvider = CreateProvider(
            WithApplicationName(connectionString, destroyApplication));
        await using AsyncServiceScope destroyScope = destroyProvider.CreateAsyncScope();
        OrganizationsDbContext destroyDbContext = destroyScope.ServiceProvider
            .GetRequiredService<OrganizationsDbContext>();
        await using var destroyTransaction = await destroyDbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);
        int destroyProcessId = BackendProcessId(destroyDbContext);
        bool destroyCommitted = false;
        try
        {
            IOrganizationScopeLifecycle lifecycle = destroyScope.ServiceProvider
                .GetRequiredService<IOrganizationScopeLifecycle>();
            OrganizationScopeDestroyResult destroying = await lifecycle
                .DestroyBatchAsync(
                    new OrganizationScopeDestroyRequest(
                        Guid.NewGuid(),
                        request.OrganizationId,
                        revision,
                        BatchSize: 1),
                    CancellationToken.None);
            Assert.Equal(
                OrganizationScopeDestroyStatus.InProgress,
                destroying.Status);
            Assert.Equal(revision + 1, destroying.Progress!.ResultingRevision);
            Assert.Equal(1, destroying.Progress.RemovedRecordCount);

            Task<OrganizationProvisioningResult> replayTask = ProvisionAsync(
                WithApplicationName(connectionString, replayApplication),
                request with { ActorId = "admin:destroy-first-recovery" });
            await WaitForAdvisoryBlockAsync(
                connectionString,
                replayApplication,
                destroyProcessId,
                replayTask);

            OrganizationProvisioningResult unrelated = await ProvisionAsync(
                    provider,
                    unrelatedRequest with
                    {
                        ActorId = "admin:unrelated-recovery"
                    })
                .WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(
                OrganizationProvisioningOutcome.AlreadyProvisioned,
                unrelated.Outcome);
            Assert.True(unrelated.IsSuccess);

            await destroyTransaction.CommitAsync();
            destroyCommitted = true;
            OrganizationProvisioningResult replay = await replayTask
                .WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(
                OrganizationProvisioningOutcome.IdentityConflict,
                replay.Outcome);
            Assert.Null(replay.Summary);
            Assert.Equal(
                OrganizationApplicationErrors.CreationOperationConflict.Code,
                replay.ErrorCode);
            await AssertClosedLiveScopeAsync(
                connectionString,
                request.OrganizationId,
                destroying.Progress.ResultingRevision);
        }
        finally
        {
            if (!destroyCommitted)
            {
                await destroyTransaction.RollbackAsync(CancellationToken.None);
            }
        }
    }

    private static async Task VerifyFreshCreationFirstAsync(
        string connectionString,
        OrganizationProvisioningRequest request)
    {
        string creatorApplication = ApplicationName("creator-holder");
        string destroyApplication = ApplicationName("empty-destroy-waiter");
        await using ServiceProvider creatorProvider = CreateProvider(
            WithApplicationName(connectionString, creatorApplication));
        await using AsyncServiceScope creatorScope =
            creatorProvider.CreateAsyncScope();
        OrganizationsDbContext creatorDbContext = creatorScope.ServiceProvider
            .GetRequiredService<OrganizationsDbContext>();
        await using var creatorTransaction = await creatorDbContext.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted);
        int creatorProcessId = BackendProcessId(creatorDbContext);
        bool creatorCommitted = false;
        try
        {
            IOrganizationProvisioner provisioner = creatorScope.ServiceProvider
                .GetRequiredService<IOrganizationProvisioner>();
            OrganizationProvisioningResult created = await provisioner
                .ProvisionAsync(request, CancellationToken.None);
            Assert.Equal(
                OrganizationProvisioningOutcome.Provisioned,
                created.Outcome);
            Assert.True(created.IsSuccess);

            Task<OrganizationScopeDestroyResult> destroyTask = DestroyAsync(
                WithApplicationName(connectionString, destroyApplication),
                new OrganizationScopeDestroyRequest(
                    Guid.NewGuid(),
                    request.OrganizationId,
                    ExpectedRevision: 0,
                    BatchSize: 1));
            await WaitForAdvisoryBlockAsync(
                connectionString,
                destroyApplication,
                creatorProcessId,
                destroyTask);

            await creatorTransaction.CommitAsync();
            creatorCommitted = true;
            OrganizationScopeDestroyResult destroyed = await destroyTask
                .WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(OrganizationScopeDestroyStatus.Stale, destroyed.Status);
            Assert.Null(destroyed.Progress);
            Assert.Null(destroyed.Receipt);
            await AssertOpenCreatedScopeAsync(
                connectionString,
                request.OrganizationId);
        }
        finally
        {
            if (!creatorCommitted)
            {
                await creatorTransaction.RollbackAsync(CancellationToken.None);
            }
        }
    }

    private static async Task WaitForAdvisoryBlockAsync(
        string connectionString,
        string waiterApplicationName,
        int blockerProcessId,
        Task waiterTask)
    {
        await using NpgsqlConnection monitor = new(connectionString);
        await monitor.OpenAsync();
        await using NpgsqlCommand command = new("""
            SELECT EXISTS (
                SELECT 1
                FROM pg_stat_activity AS activity
                INNER JOIN pg_locks AS waiting
                    ON waiting.pid = activity.pid
                    AND waiting.locktype = 'advisory'
                    AND NOT waiting.granted
                INNER JOIN pg_locks AS held
                    ON held.pid = @blocker_process_id
                    AND held.locktype = 'advisory'
                    AND held.granted
                    AND held.database IS NOT DISTINCT FROM waiting.database
                    AND held.classid = waiting.classid
                    AND held.objid = waiting.objid
                    AND held.objsubid = waiting.objsubid
                WHERE activity.application_name = @waiter_application_name
                    AND @blocker_process_id = ANY(pg_blocking_pids(activity.pid))
            );
            """, monitor);
        command.Parameters.AddWithValue(
            "waiter_application_name",
            waiterApplicationName);
        command.Parameters.AddWithValue("blocker_process_id", blockerProcessId);

        using CancellationTokenSource timeout =
            new(TimeSpan.FromSeconds(10));
        try
        {
            while (true)
            {
                Assert.False(
                    waiterTask.IsCompleted,
                    "The competing operation completed before reaching the expected advisory-lock barrier.");
                object? scalar = await command.ExecuteScalarAsync(timeout.Token);
                if (scalar is true)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            Assert.Fail(
                $"Connection '{waiterApplicationName}' did not wait on the expected advisory lock.");
        }
    }

    private static async Task AssertClosedLiveScopeAsync(
        string connectionString,
        Guid organizationId,
        long expectedRevision)
    {
        await using ServiceProvider provider = CreateProvider(connectionString);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        OrganizationsDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<OrganizationsDbContext>();
        Assert.True(await dbContext.Organizations.AnyAsync(candidate =>
            candidate.Id == organizationId));
        Assert.True(await dbContext.Memberships.AnyAsync(candidate =>
            candidate.OrganizationId == organizationId));
        var state = await dbContext.OrganizationScopeStates.SingleAsync(candidate =>
            candidate.OrganizationId == organizationId);
        Assert.True(state.IsClosed);
        Assert.Equal(expectedRevision, state.Version);
    }

    private static async Task AssertOpenCreatedScopeAsync(
        string connectionString,
        Guid organizationId)
    {
        await using ServiceProvider provider = CreateProvider(connectionString);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        OrganizationsDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<OrganizationsDbContext>();
        Assert.Single(await dbContext.Organizations.Where(candidate =>
            candidate.Id == organizationId).ToArrayAsync());
        Assert.Single(await dbContext.Memberships.Where(candidate =>
            candidate.OrganizationId == organizationId).ToArrayAsync());
        Assert.Equal(2, await dbContext.OutboxMessages.CountAsync(candidate =>
            candidate.ScopeId == organizationId.ToString("D")));
        var state = await dbContext.OrganizationScopeStates.SingleAsync(candidate =>
            candidate.OrganizationId == organizationId);
        Assert.False(state.IsClosed);
        Assert.Equal(1, state.Version);
        Assert.Empty(await dbContext.OrganizationScopeDestroyOperations
            .Where(candidate => candidate.OrganizationId == organizationId)
            .ToArrayAsync());
        Assert.Empty(await dbContext.OrganizationScopeDestroyReceipts
            .Where(candidate => candidate.OrganizationId == organizationId)
            .ToArrayAsync());
    }

    private static async Task<long> OpenRevisionAsync(
        string connectionString,
        Guid organizationId)
    {
        await using ServiceProvider provider = CreateProvider(connectionString);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IOrganizationScopeLifecycle lifecycle = scope.ServiceProvider
            .GetRequiredService<IOrganizationScopeLifecycle>();
        OrganizationScopeSnapshot snapshot = await lifecycle.GetSnapshotAsync(
            organizationId,
            CancellationToken.None);
        Assert.Equal(OrganizationScopeStatus.Open, snapshot.Status);
        return snapshot.Revision;
    }

    private static async Task AssertProvisionedAsync(
        ServiceProvider provider,
        OrganizationProvisioningRequest request)
    {
        OrganizationProvisioningResult result = await ProvisionAsync(
            provider,
            request);
        Assert.Equal(OrganizationProvisioningOutcome.Provisioned, result.Outcome);
        Assert.True(result.IsSuccess);
    }

    private static async Task<OrganizationProvisioningResult> ProvisionAsync(
        ServiceProvider provider,
        OrganizationProvisioningRequest request)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IOrganizationProvisioner provisioner = scope.ServiceProvider
            .GetRequiredService<IOrganizationProvisioner>();
        return await provisioner.ProvisionAsync(request, CancellationToken.None);
    }

    private static async Task<OrganizationProvisioningResult> ProvisionAsync(
        string connectionString,
        OrganizationProvisioningRequest request)
    {
        await using ServiceProvider provider = CreateProvider(connectionString);
        return await ProvisionAsync(provider, request);
    }

    private static async Task<OrganizationScopeDestroyResult> DestroyAsync(
        string connectionString,
        OrganizationScopeDestroyRequest request)
    {
        await using ServiceProvider provider = CreateProvider(connectionString);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IOrganizationScopeLifecycle lifecycle = scope.ServiceProvider
            .GetRequiredService<IOrganizationScopeLifecycle>();
        return await lifecycle.DestroyBatchAsync(request, CancellationToken.None);
    }

    private static OrganizationProvisioningRequest Request(
        Guid organizationId,
        string name,
        string slug,
        string ownerSubjectId) =>
        new(
            organizationId,
            name,
            slug,
            ownerSubjectId,
            "admin:initial");

    private static int BackendProcessId(OrganizationsDbContext dbContext) =>
        ((NpgsqlConnection)dbContext.Database.GetDbConnection()).ProcessID;

    private static string ApplicationName(string role) =>
        $"gma-org-{role}-{Guid.NewGuid():N}";

    private static string WithApplicationName(
        string connectionString,
        string applicationName)
    {
        NpgsqlConnectionStringBuilder builder = new(connectionString)
        {
            ApplicationName = applicationName
        };
        return builder.ConnectionString;
    }

    private static ServiceProvider CreateProvider(string connectionString)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = "PostgreSql",
            ["ConnectionStrings:PostgreSql"] = connectionString,
            ["Organizations:SelfServiceCreationEnabled"] = "false",
            ["Organizations:Lifecycle:Enabled"] = "false",
            ["Organizations:Retention:Enabled"] = "false"
        });
        builder.Services.AddSingleton<ISystemClock>(new FixedClock());
        builder.Services.AddSingleton<IIdGenerator, TestIdGenerator>();
        builder.AddCqrsInfrastructure();
        builder.AddApplicationEventsInfrastructure();
        builder.AddMessagingInfrastructure();
        builder.Services.AddOrganizationsApplication(builder.Configuration);
        builder.Services.AddOrganizationProvisioning();
        builder.AddOrganizationsPersistence();
        return builder.Services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task MigrateAsync(ServiceProvider provider)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        OrganizationsDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<OrganizationsDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    private sealed class FixedClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }
}
