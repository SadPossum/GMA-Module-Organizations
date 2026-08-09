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

        await using AsyncServiceScope verificationScope =
            provider.CreateAsyncScope();
        OrganizationsDbContext dbContext = verificationScope.ServiceProvider
            .GetRequiredService<OrganizationsDbContext>();
        Assert.Equal(4, await dbContext.Organizations.CountAsync());
        Assert.Equal(4, await dbContext.Memberships.CountAsync());
        Assert.Single(await dbContext.Organizations
            .Where(organization => organization.Id == exactOperationId)
            .ToArrayAsync());
        Assert.Single(await dbContext.Organizations
            .Where(organization => organization.Id == changedOperationId)
            .ToArrayAsync());
    }

    private static ServiceProvider CreateProvider(string connectionString)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = "PostgreSql",
            ["ConnectionStrings:PostgreSql"] = connectionString,
            ["Organizations:SelfServiceCreationEnabled"] = "true",
            ["Organizations:Lifecycle:Enabled"] = "false",
            ["Organizations:Retention:Enabled"] = "false"
        });
        builder.Services.AddSingleton<ISystemClock>(new FixedClock());
        builder.Services.AddSingleton<IIdGenerator, TestIdGenerator>();
        builder.AddCqrsInfrastructure();
        builder.AddApplicationEventsInfrastructure();
        builder.AddMessagingInfrastructure();
        builder.Services.AddOrganizationsApplication(builder.Configuration);
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
}
