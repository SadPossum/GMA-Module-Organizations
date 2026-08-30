namespace Gma.Modules.Organizations.IntegrationTests;

using Gma.Framework.Application.Events.Infrastructure;
using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Entities;
using Gma.Modules.Organizations.IntegrationTests.Support;
using Gma.Modules.Organizations.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

[Trait("Category", "Docker")]
[Trait("Category", "Integration")]
public sealed class OrganizationCreationPostgreSqlIntegrationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    [DockerFact]
    public async Task Creation_is_serialized_per_operation_and_exactly_replayable()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("organization_creation_tests")
                .Build();
        await postgreSql.StartAsync();
        await using ServiceProvider provider =
            CreateProvider(postgreSql.GetConnectionString());
        await MigrateAsync(provider);

        Guid exactOperationId = Guid.NewGuid();
        CreateOrganizationCommand exactCommand = CreateCommand(
            exactOperationId,
            "Harbor House",
            "harbor-house",
            "subject-a");
        Result<OrganizationMembershipSummaryDto>[] exactResults =
            await Task.WhenAll(
                DispatchAsync(provider, exactCommand),
                DispatchAsync(provider, exactCommand));

        Assert.All(exactResults, result => Assert.True(result.IsSuccess, result.Error.Code));
        Assert.Single(exactResults
            .Select(result => result.Value.Organization.OrganizationId)
            .Distinct());
        Assert.Single(exactResults
            .Select(result => result.Value.Membership.MembershipId)
            .Distinct());

        Guid changedOperationId = Guid.NewGuid();
        Result<OrganizationMembershipSummaryDto>[] changedResults =
            await Task.WhenAll(
                DispatchAsync(provider, CreateCommand(
                    changedOperationId,
                    "North House",
                    "north-house",
                    "subject-b")),
                DispatchAsync(provider, CreateCommand(
                    changedOperationId,
                    "South House",
                    "south-house",
                    "subject-b")));

        Assert.Single(changedResults, result => result.IsSuccess);
        Result<OrganizationMembershipSummaryDto> changedFailure = Assert.Single(
            changedResults,
            result => result.IsFailure);
        Assert.Equal(
            OrganizationApplicationErrors.CreationOperationConflict.Code,
            changedFailure.Error.Code);

        Result<OrganizationMembershipSummaryDto>[] independentResults =
            await Task.WhenAll(
                DispatchAsync(provider, CreateCommand(
                    Guid.NewGuid(),
                    "East House",
                    "east-house",
                    "subject-c")),
                DispatchAsync(provider, CreateCommand(
                    Guid.NewGuid(),
                    "West House",
                    "west-house",
                    "subject-d")));
        Assert.All(
            independentResults,
            result => Assert.True(result.IsSuccess, result.Error.Code));

        Guid crossChannelOperationId = Guid.NewGuid();
        Task<Result<OrganizationMembershipSummaryDto>> selfService = DispatchAsync(
            provider,
            CreateCommand(
                crossChannelOperationId,
                "Cross Channel House",
                "cross-channel-house",
                "subject-e"));
        Task<OrganizationProvisioningResult> trusted = ProvisionAsync(
            provider,
            new OrganizationProvisioningRequest(
                crossChannelOperationId,
                "Cross Channel House",
                "cross-channel-house",
                "subject-e",
                "admin:cross-channel"));
        await Task.WhenAll(selfService, trusted);
        Assert.NotEqual(selfService.Result.IsSuccess, trusted.Result.IsSuccess);
        if (selfService.Result.IsSuccess)
        {
            Assert.Equal(
                OrganizationProvisioningOutcome.IdentityConflict,
                trusted.Result.Outcome);
        }
        else
        {
            Assert.Equal(
                OrganizationApplicationErrors.CreationOperationConflict,
                selfService.Result.Error);
            Assert.True(trusted.Result.IsSuccess);
        }

        await using AsyncServiceScope verificationScope =
            provider.CreateAsyncScope();
        OrganizationsDbContext dbContext = verificationScope.ServiceProvider
            .GetRequiredService<OrganizationsDbContext>();
        Assert.Equal(5, await dbContext.Organizations.CountAsync());
        Assert.Equal(5, await dbContext.Memberships.CountAsync());
        Assert.Single(await dbContext.Organizations
            .Where(organization => organization.Id == exactOperationId)
            .ToArrayAsync());
        Assert.Single(await dbContext.Organizations
            .Where(organization => organization.Id == changedOperationId)
            .ToArrayAsync());
        Assert.Single(await dbContext.Organizations
            .Where(organization => organization.Id == crossChannelOperationId)
            .ToArrayAsync());
    }

    [DockerFact]
    public async Task Trusted_provisioning_is_recoverable_isolated_and_tombstone_safe()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("organization_provisioning_tests")
                .Build();
        await postgreSql.StartAsync();
        RecordingCreationPolicy policy = new();
        await using ServiceProvider provider = CreateProvider(
            postgreSql.GetConnectionString(),
            selfServiceCreationEnabled: false,
            policy);
        await MigrateAsync(provider);

        Guid organizationId = Guid.NewGuid();
        OrganizationProvisioningRequest request = new(
            organizationId,
            "Harbor House",
            "harbor-house",
            "owner-a",
            "admin:first");
        OrganizationProvisioningResult[] exact = await Task.WhenAll(
            ProvisionAsync(provider, request),
            ProvisionAsync(provider, request with { ActorId = "admin:repair" }));

        Assert.All(exact, result => Assert.True(result.IsSuccess));
        Assert.Single(exact, result =>
            result.Outcome == OrganizationProvisioningOutcome.Provisioned);
        Assert.Single(exact, result =>
            result.Outcome ==
                OrganizationProvisioningOutcome.AlreadyProvisioned);
        Assert.Single(exact
            .Select(result => result.Summary!.Organization.OrganizationId)
            .Distinct());
        Assert.Single(exact
            .Select(result => result.Summary!.Membership.MembershipId)
            .Distinct());
        Assert.Equal(0, policy.InvocationCount);

        int outboxAfterCreation;
        await using (AsyncServiceScope verificationScope =
                     provider.CreateAsyncScope())
        {
            OrganizationsDbContext database = verificationScope.ServiceProvider
                .GetRequiredService<OrganizationsDbContext>();
            Assert.Equal(1, await database.Organizations.CountAsync());
            Assert.Equal(1, await database.Memberships.CountAsync());
            Assert.Single(await database.OrganizationScopeStates.Where(state =>
                state.OrganizationId == organizationId && !state.IsClosed)
                .ToArrayAsync());
            outboxAfterCreation = await database.OutboxMessages.CountAsync(
                message => message.ScopeId == organizationId.ToString("D"));
            Assert.Equal(2, outboxAfterCreation);
        }

        OrganizationProvisioningResult delayed = await ProvisionAsync(
            provider,
            request with { ActorId = "admin:third" });
        Assert.Equal(
            OrganizationProvisioningOutcome.AlreadyProvisioned,
            delayed.Outcome);
        await using (AsyncServiceScope verificationScope =
                     provider.CreateAsyncScope())
        {
            OrganizationsDbContext database = verificationScope.ServiceProvider
                .GetRequiredService<OrganizationsDbContext>();
            Assert.Equal(
                outboxAfterCreation,
                await database.OutboxMessages.CountAsync(message =>
                    message.ScopeId == organizationId.ToString("D")));
        }

        Guid divergentId = Guid.NewGuid();
        OrganizationProvisioningResult[] divergent = await Task.WhenAll(
            ProvisionAsync(provider, new OrganizationProvisioningRequest(
                divergentId,
                "North House",
                "north-house",
                "owner-b",
                "admin:north")),
            ProvisionAsync(provider, new OrganizationProvisioningRequest(
                divergentId,
                "South House",
                "south-house",
                "owner-b",
                "admin:south")));
        Assert.Single(divergent, result => result.IsSuccess);
        Assert.Single(divergent, result =>
            result.Outcome == OrganizationProvisioningOutcome.IdentityConflict);

        OrganizationProvisioningResult[] sameSlug = await Task.WhenAll(
            ProvisionAsync(provider, new OrganizationProvisioningRequest(
                Guid.NewGuid(),
                "East House",
                "shared-house",
                "owner-c",
                "admin:east")),
            ProvisionAsync(provider, new OrganizationProvisioningRequest(
                Guid.NewGuid(),
                "West House",
                "shared-house",
                "owner-d",
                "admin:west")));
        Assert.Single(sameSlug, result => result.IsSuccess);
        Assert.Single(sameSlug, result =>
            result.Outcome == OrganizationProvisioningOutcome.SlugConflict);

        Guid crossChannelId = Guid.NewGuid();
        Task<Result<OrganizationMembershipSummaryDto>> selfService = DispatchAsync(
            provider,
            CreateCommand(
                crossChannelId,
                "Cross Channel House",
                "cross-channel-house",
                "owner-e"));
        Task<OrganizationProvisioningResult> trusted = ProvisionAsync(
            provider,
            new OrganizationProvisioningRequest(
                crossChannelId,
                "Cross Channel House",
                "cross-channel-house",
                "owner-e",
                "admin:cross-channel"));
        await Task.WhenAll(selfService, trusted);
        Assert.False(selfService.Result.IsSuccess);
        Assert.Equal(
            OrganizationApplicationErrors.SelfServiceCreationDisabled,
            selfService.Result.Error);
        Assert.True(trusted.Result.IsSuccess);

        Guid closingId = Guid.NewGuid();
        OrganizationProvisioningRequest closingRequest = new(
            closingId,
            "Closing House",
            "closing-house",
            "owner-closing",
            "admin:closing");
        Assert.True((await ProvisionAsync(provider, closingRequest)).IsSuccess);
        await using (AsyncServiceScope closingScope =
                     provider.CreateAsyncScope())
        {
            OrganizationsDbContext database = closingScope.ServiceProvider
                .GetRequiredService<OrganizationsDbContext>();
            OrganizationScopeState closingState = await database
                .OrganizationScopeStates
                .SingleAsync(state => state.OrganizationId == closingId);
            Assert.Equal(
                Gma.Modules.Organizations.Domain.Entities
                    .OrganizationScopeCloseTransition.Completed,
                closingState.Close(
                    Guid.NewGuid(),
                    new string('b', 64),
                    Now));
            await database.SaveChangesAsync();
            Assert.True(await database.Organizations.AnyAsync(organization =>
                organization.Id == closingId));
            Assert.True(await database.Memberships.AnyAsync(membership =>
                membership.OrganizationId == closingId));
        }

        OrganizationProvisioningResult closingReplay = await ProvisionAsync(
            provider,
            closingRequest with { ActorId = "admin:closing-repair" });
        Assert.Equal(
            OrganizationProvisioningOutcome.IdentityConflict,
            closingReplay.Outcome);
        Assert.Null(closingReplay.Summary);

        Guid closedId = Guid.NewGuid();
        await using (AsyncServiceScope tombstoneScope =
                     provider.CreateAsyncScope())
        {
            OrganizationsDbContext database = tombstoneScope.ServiceProvider
                .GetRequiredService<OrganizationsDbContext>();
            OrganizationScopeState tombstone =
                OrganizationScopeState.Create(closedId).Value;
            Assert.Equal(
                Gma.Modules.Organizations.Domain.Entities
                    .OrganizationScopeCloseTransition.Completed,
                tombstone.Close(
                    Guid.NewGuid(),
                    new string('a', 64),
                    Now));
            await database.OrganizationScopeStates.AddAsync(tombstone);
            await database.SaveChangesAsync();
        }

        OrganizationProvisioningResult closed = await ProvisionAsync(
            provider,
            new OrganizationProvisioningRequest(
                closedId,
                "Closed House",
                "closed-house",
                "owner-f",
                "admin:closed"));
        Assert.Equal(
            OrganizationProvisioningOutcome.IdentityConflict,
            closed.Outcome);
        Assert.Null(closed.Summary);
    }

    private static ServiceProvider CreateProvider(
        string connectionString,
        bool selfServiceCreationEnabled = true,
        IOrganizationCreationAdmissionPolicy? creationPolicy = null)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = "PostgreSql",
            ["ConnectionStrings:PostgreSql"] = connectionString,
            ["Organizations:SelfServiceCreationEnabled"] =
                selfServiceCreationEnabled.ToString(),
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
        if (creationPolicy is not null)
        {
            builder.Services.AddSingleton(creationPolicy);
        }
        builder.AddOrganizationsPersistence();
        return builder.Services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task MigrateAsync(ServiceProvider provider)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        OrganizationsDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OrganizationsDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    private static async Task<Result<OrganizationMembershipSummaryDto>> DispatchAsync(
        ServiceProvider provider,
        CreateOrganizationCommand command)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IRequestDispatcher dispatcher =
            scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        return await dispatcher.SendAsync(command, CancellationToken.None);
    }

    private static async Task<OrganizationProvisioningResult> ProvisionAsync(
        ServiceProvider provider,
        OrganizationProvisioningRequest request)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IOrganizationProvisioner provisioner = scope.ServiceProvider
            .GetRequiredService<IOrganizationProvisioner>();
        Assert.Single(scope.ServiceProvider.GetServices<IOrganizationProvisioner>());
        return await provisioner.ProvisionAsync(request, CancellationToken.None);
    }

    private static CreateOrganizationCommand CreateCommand(
        Guid operationId,
        string name,
        string slug,
        string subjectId) =>
        new(operationId, name, slug, subjectId, $"user:{subjectId}");

    private sealed class FixedClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }

    private sealed class RecordingCreationPolicy
        : IOrganizationCreationAdmissionPolicy
    {
        public int InvocationCount { get; private set; }

        public ValueTask<OrganizationCreationAdmissionDecision> EvaluateAsync(
            OrganizationCreationAdmissionRequest request,
            CancellationToken cancellationToken = default)
        {
            this.InvocationCount++;
            throw new InvalidOperationException(
                "Trusted provisioning must not invoke self-service admission.");
        }
    }
}
