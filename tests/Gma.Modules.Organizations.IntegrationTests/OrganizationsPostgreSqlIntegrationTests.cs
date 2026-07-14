namespace Gma.Modules.Organizations.IntegrationTests;

using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.IntegrationTests.Support;
using Gma.Modules.Organizations.Persistence;
using Gma.Modules.Organizations.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

[Trait("Category", "Docker")]
[Trait("Category", "Integration")]
public sealed class OrganizationsPostgreSqlIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    [DockerFact]
    public async Task Concurrent_claims_leave_one_winner_without_exceeding_capacity()
    {
        await using PostgreSqlContainer postgreSql = CreatePostgreSql("organizations_race_tests");
        await postgreSql.StartAsync();
        (Organization organization, OrganizationEnrollmentLink link) =
            await SeedEnrollmentAsync(postgreSql.GetConnectionString(), maximumClaims: 1);

        await using OrganizationsDbContext first = CreateDbContext(postgreSql.GetConnectionString());
        await using OrganizationsDbContext second = CreateDbContext(postgreSql.GetConnectionString());
        OrganizationEnrollmentLink firstLink = await first.EnrollmentLinks.SingleAsync();
        OrganizationEnrollmentLink secondLink = await second.EnrollmentLinks.SingleAsync();
        Assert.True(firstLink.ReserveClaim("user:first", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
        Assert.True(secondLink.ReserveClaim("user:second", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
        first.EnrollmentClaims.Add(CreatePendingClaim(organization.Id, link.Id, "first"));
        second.EnrollmentClaims.Add(CreatePendingClaim(organization.Id, link.Id, "second"));

        Task<Exception?> firstSave = CaptureAsync(() => first.SaveChangesAsync());
        Task<Exception?> secondSave = CaptureAsync(() => second.SaveChangesAsync());
        Exception?[] failures = await Task.WhenAll(firstSave, secondSave);

        Assert.Single(failures, failure => failure is null);
        Assert.Single(failures, failure => failure is DbUpdateConcurrencyException);
        await using OrganizationsDbContext verification = CreateDbContext(postgreSql.GetConnectionString());
        Assert.Equal(1, (await verification.EnrollmentLinks.SingleAsync()).ReservedClaims);
        Assert.Single(await verification.EnrollmentClaims.ToArrayAsync());
    }

    [DockerFact]
    public async Task Membership_discovery_stays_subject_and_organization_isolated_on_postgresql()
    {
        await using PostgreSqlContainer postgreSql = CreatePostgreSql("organizations_isolation_tests");
        await postgreSql.StartAsync();
        await using OrganizationsDbContext dbContext = CreateDbContext(postgreSql.GetConnectionString());
        await dbContext.Database.MigrateAsync();
        Organization first = CreateOrganization("First House", "first-house");
        Organization second = CreateOrganization("Second House", "second-house");
        dbContext.Organizations.AddRange(first, second);
        dbContext.Memberships.AddRange(
            CreateMembership(first.Id, "subject-a"),
            CreateMembership(second.Id, "subject-a"),
            CreateMembership(second.Id, "subject-b"));
        await dbContext.SaveChangesAsync();
        OrganizationRepository repository = new(dbContext);

        var discovered = await repository.ListForSubjectAsync(
            "subject-a", page: 1, pageSize: 25, CancellationToken.None);
        OrganizationMembership? scoped = await repository.GetMembershipAsync(
            first.Id, "subject-b", CancellationToken.None);

        Assert.Equal(2, discovered.Items.Count);
        Assert.All(discovered.Items, item => Assert.Equal("subject-a", item.Membership.SubjectId));
        Assert.Null(scoped);
    }

    private static async Task<(Organization Organization, OrganizationEnrollmentLink Link)> SeedEnrollmentAsync(
        string connectionString,
        int maximumClaims)
    {
        await using OrganizationsDbContext dbContext = CreateDbContext(connectionString);
        await dbContext.Database.MigrateAsync();
        Organization organization = CreateOrganization("Harbor House", "harbor-house");
        OrganizationMembership owner = OrganizationMembership.Create(
            Guid.NewGuid(), organization.Id, "owner", OrganizationMembershipRole.Owner,
            "user:owner", Guid.NewGuid(), Now).Value;
        OrganizationEnrollmentLink link = OrganizationEnrollmentLink.Create(
            Guid.NewGuid(), organization.Id, "owner", new string('a', 64),
            Now.AddDays(1), maximumClaims, OrganizationEnrollmentApprovalMode.RequiresApproval,
            "user:owner", Guid.NewGuid(), Now).Value;
        dbContext.AddRange(organization, owner, link);
        await dbContext.SaveChangesAsync();
        return (organization, link);
    }

    private static OrganizationEnrollmentClaim CreatePendingClaim(
        Guid organizationId,
        Guid linkId,
        string subjectId) => OrganizationEnrollmentClaim.Create(
            Guid.NewGuid(), organizationId, linkId, subjectId,
            OrganizationEnrollmentClaimState.Pending, null,
            $"user:{subjectId}", Guid.NewGuid(), Now.AddMinutes(1)).Value;

    private static Organization CreateOrganization(string name, string slug) => Organization.Create(
        Guid.NewGuid(), name, slug, "user:owner", Guid.NewGuid(), Now).Value;

    private static OrganizationMembership CreateMembership(Guid organizationId, string subjectId) =>
        OrganizationMembership.Create(
            Guid.NewGuid(), organizationId, subjectId, OrganizationMembershipRole.Member,
            "user:owner", Guid.NewGuid(), Now).Value;

    private static OrganizationsDbContext CreateDbContext(string connectionString)
    {
        DbContextOptions<OrganizationsDbContext> options =
            new DbContextOptionsBuilder<OrganizationsDbContext>()
                .UseNpgsql(connectionString, provider => provider
                    .MigrationsAssembly(OrganizationsMigrations.PostgreSqlAssembly)
                    .MigrationsHistoryTable(
                        OrganizationsMigrations.HistoryTable,
                        OrganizationsMigrations.Schema))
                .Options;
        return new OrganizationsDbContext(options);
    }

    private static PostgreSqlContainer CreatePostgreSql(string database) =>
        new PostgreSqlBuilder("postgres:16-alpine").WithDatabase(database).Build();

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
}
