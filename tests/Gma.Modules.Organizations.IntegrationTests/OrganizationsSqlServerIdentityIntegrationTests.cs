namespace Gma.Modules.Organizations.IntegrationTests;

using System.Data;
using System.Data.Common;
using Gma.Framework.Pagination;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.IntegrationTests.Support;
using Gma.Modules.Organizations.Persistence;
using Gma.Modules.Organizations.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

[Trait("Category", "Docker")]
[Trait("Category", "Integration")]
public sealed class OrganizationsSqlServerIdentityIntegrationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 9, 13, 0, 0, TimeSpan.Zero);

    [DockerFact]
    public async Task Case_distinct_subjects_and_actors_remain_exact_on_sql_server()
    {
        await using MsSqlContainer sqlServer = new MsSqlBuilder(
                "mcr.microsoft.com/mssql/server:2022-latest")
            .Build();
        await sqlServer.StartAsync();

        await using OrganizationsDbContext dbContext = CreateDbContext(
            sqlServer.GetConnectionString());
        await dbContext.Database.MigrateAsync();
        await VerifyMembershipExportIndexAsync(dbContext);

        Organization organization = Organization.Create(
            Guid.NewGuid(),
            "Ordinal House",
            "ordinal-house",
            "Actor-Owner",
            Guid.NewGuid(),
            Now).Value;
        OrganizationMembership owner = OrganizationMembership.Create(
            Guid.NewGuid(),
            organization.Id,
            "owner",
            OrganizationMembershipRole.Owner,
            "Actor-Owner",
            Guid.NewGuid(),
            Now).Value;
        OrganizationMembership upperMembership = OrganizationMembership.Create(
            Guid.NewGuid(),
            organization.Id,
            "Case-Subject",
            OrganizationMembershipRole.Member,
            "Actor-Case",
            Guid.NewGuid(),
            Now).Value;
        OrganizationMembership lowerMembership = OrganizationMembership.Create(
            Guid.NewGuid(),
            organization.Id,
            "case-subject",
            OrganizationMembershipRole.Member,
            "actor-case",
            Guid.NewGuid(),
            Now).Value;
        OrganizationEnrollmentLink link = OrganizationEnrollmentLink.Create(
            Guid.NewGuid(),
            organization.Id,
            owner.SubjectId,
            new string('a', 64),
            Now.AddDays(1),
            2,
            OrganizationEnrollmentApprovalMode.RequiresApproval,
            "Actor-Owner",
            Guid.NewGuid(),
            Now).Value;
        Assert.True(link.ReserveClaim(
            "Actor-Case", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
        Assert.True(link.ReserveClaim(
            "actor-case", Guid.NewGuid(), Now.AddMinutes(2)).IsSuccess);
        OrganizationEnrollmentClaim upperClaim = CreatePendingClaim(
            organization.Id,
            link.Id,
            upperMembership.SubjectId,
            "Actor-Case",
            Now.AddMinutes(1));
        OrganizationEnrollmentClaim lowerClaim = CreatePendingClaim(
            organization.Id,
            link.Id,
            lowerMembership.SubjectId,
            "actor-case",
            Now.AddMinutes(2));

        dbContext.AddRange(
            organization,
            owner,
            upperMembership,
            lowerMembership,
            link,
            upperClaim,
            lowerClaim);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        OrganizationRepository repository = new(dbContext);
        OrganizationMembership? storedUpper = await repository
            .GetMembershipAsync(
                organization.Id,
                upperMembership.SubjectId,
                CancellationToken.None);
        OrganizationMembership? storedLower = await repository
            .GetMembershipAsync(
                organization.Id,
                lowerMembership.SubjectId,
                CancellationToken.None);
        OrganizationEnrollmentClaim? storedUpperClaim = await repository
            .GetEnrollmentClaimBySubjectAsync(
                link.Id,
                upperClaim.SubjectId,
                CancellationToken.None);
        OrganizationEnrollmentClaim? storedLowerClaim = await repository
            .GetEnrollmentClaimBySubjectAsync(
                link.Id,
                lowerClaim.SubjectId,
                CancellationToken.None);

        Assert.Equal(upperMembership.Id, storedUpper?.Id);
        Assert.Equal(lowerMembership.Id, storedLower?.Id);
        Assert.Equal(upperClaim.Id, storedUpperClaim?.Id);
        Assert.Equal(lowerClaim.Id, storedLowerClaim?.Id);
        Assert.Single((await repository.ListForSubjectAsync(
            upperMembership.SubjectId,
            PageRequest.Normalize(1, 25),
            CancellationToken.None)).Items);
        Assert.Single((await repository.ListForSubjectAsync(
            lowerMembership.SubjectId,
            PageRequest.Normalize(1, 25),
            CancellationToken.None)).Items);
        Assert.Equal(1, await dbContext.Memberships.CountAsync(
            membership => membership.CreatedBy == "Actor-Case"));
        Assert.Equal(1, await dbContext.Memberships.CountAsync(
            membership => membership.CreatedBy == "actor-case"));
    }

    private static async Task VerifyMembershipExportIndexAsync(
        OrganizationsDbContext dbContext)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        bool shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT STRING_AGG(column_definition.name, ',')
                    WITHIN GROUP (ORDER BY index_column.key_ordinal)
                FROM sys.indexes AS index_definition
                INNER JOIN sys.tables AS table_definition
                    ON table_definition.object_id =
                        index_definition.object_id
                INNER JOIN sys.schemas AS schema_definition
                    ON schema_definition.schema_id =
                        table_definition.schema_id
                INNER JOIN sys.index_columns AS index_column
                    ON index_column.object_id = index_definition.object_id
                    AND index_column.index_id = index_definition.index_id
                    AND index_column.key_ordinal > 0
                INNER JOIN sys.columns AS column_definition
                    ON column_definition.object_id = index_column.object_id
                    AND column_definition.column_id = index_column.column_id
                WHERE schema_definition.name = 'organizations'
                    AND table_definition.name = 'organization_memberships'
                    AND index_definition.name =
                        'IX_organization_memberships_OrganizationId_Id'
                GROUP BY index_definition.name;
                """;
            string? columns = (string?)await command.ExecuteScalarAsync();
            Assert.Equal("OrganizationId,Id", columns);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static OrganizationEnrollmentClaim CreatePendingClaim(
        Guid organizationId,
        Guid linkId,
        string subjectId,
        string actorId,
        DateTimeOffset createdAtUtc) =>
        OrganizationEnrollmentClaim.Create(
            Guid.NewGuid(),
            organizationId,
            linkId,
            subjectId,
            OrganizationEnrollmentClaimState.Pending,
            null,
            actorId,
            Guid.NewGuid(),
            createdAtUtc,
            createdAtUtc.AddDays(7)).Value;

    private static OrganizationsDbContext CreateDbContext(
        string connectionString)
    {
        DbContextOptions<OrganizationsDbContext> options =
            new DbContextOptionsBuilder<OrganizationsDbContext>()
                .UseSqlServer(connectionString, provider => provider
                    .MigrationsAssembly(
                        OrganizationsMigrations.SqlServerAssembly)
                    .MigrationsHistoryTable(
                        OrganizationsMigrations.HistoryTable,
                        OrganizationsMigrations.Schema))
                .Options;
        return new OrganizationsDbContext(options);
    }
}
