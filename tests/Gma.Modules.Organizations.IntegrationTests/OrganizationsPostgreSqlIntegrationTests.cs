namespace Gma.Modules.Organizations.IntegrationTests;

using System.Data.Common;
using Gma.Framework.Pagination;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.IntegrationTests.Support;
using Gma.Modules.Organizations.Persistence;
using Gma.Modules.Organizations.Persistence.Access;
using Gma.Modules.Organizations.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using Xunit;
using ContractEnrollmentClaimDto =
    Gma.Modules.Organizations.Contracts.OrganizationEnrollmentClaimDto;
using ContractEnrollmentClaimStatus =
    Gma.Modules.Organizations.Contracts.OrganizationEnrollmentClaimStatus;
using OrganizationAccessDecision =
    Gma.Modules.Organizations.Contracts.OrganizationAccessDecision;

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

        var firstPage = await repository.ListForSubjectAsync(
            "subject-a", PageRequest.Normalize(1, 1), CancellationToken.None);
        var lastPage = await repository.ListForSubjectAsync(
            "subject-a", PageRequest.Normalize(2, 1), CancellationToken.None);
        OrganizationMembership? scoped = await repository.GetMembershipAsync(
            first.Id, "subject-b", CancellationToken.None);

        Assert.Single(firstPage.Items);
        Assert.True(firstPage.HasMore);
        Assert.Single(lastPage.Items);
        Assert.False(lastPage.HasMore);
        Assert.All(
            firstPage.Items.Concat(lastPage.Items),
            item => Assert.Equal("subject-a", item.Membership.SubjectId));
        Assert.Null(scoped);
    }

    [DockerFact]
    public async Task Access_readers_use_one_query_and_observe_membership_revocation()
    {
        await using PostgreSqlContainer postgreSql = CreatePostgreSql("organizations_access_tests");
        await postgreSql.StartAsync();
        string connectionString = postgreSql.GetConnectionString();
        Organization organization = CreateOrganization("Harbor House", "harbor-house");
        OrganizationMembership membership = CreateMembership(organization.Id, "subject-a");
        await using (OrganizationsDbContext seed = CreateDbContext(connectionString))
        {
            await seed.Database.MigrateAsync();
            seed.AddRange(organization, membership);
            await seed.SaveChangesAsync();
        }

        CountingCommandInterceptor commands = new();
        await using OrganizationsDbContext readerContext = CreateDbContext(connectionString, commands);
        OrganizationAccessDecisionReader reader = new(readerContext);

        Assert.Equal(
            OrganizationAccessDecision.Allowed,
            await reader.ReadAsync(organization.Id, "subject-a", CancellationToken.None));
        Assert.Equal(1, commands.ReaderCommands);
        Assert.Empty(readerContext.ChangeTracker.Entries());
        Assert.Equal(
            ["subject-a"],
            await reader.FilterAllowedAsync(
                organization.Id,
                ["missing", "subject-a"],
                CancellationToken.None));
        Assert.Equal(2, commands.ReaderCommands);
        Assert.Empty(readerContext.ChangeTracker.Entries());

        await using (OrganizationsDbContext writer = CreateDbContext(connectionString))
        {
            OrganizationMembership storedMembership = await writer.Memberships.SingleAsync();
            Assert.True(storedMembership.Suspend(
                storedMembership.Version,
                "user:owner",
                Guid.NewGuid(),
                Now.AddMinutes(1)).IsSuccess);
            await writer.SaveChangesAsync();
        }

        Assert.Equal(
            OrganizationAccessDecision.MembershipInactive,
            await reader.ReadAsync(organization.Id, "subject-a", CancellationToken.None));
        Assert.Equal(3, commands.ReaderCommands);
        Assert.Empty(await reader.FilterAllowedAsync(
            organization.Id,
            ["subject-a"],
            CancellationToken.None));
        Assert.Equal(4, commands.ReaderCommands);
        Assert.Empty(readerContext.ChangeTracker.Entries());
    }

    [DockerFact]
    public async Task Enrollment_claim_inspector_uses_one_exact_untracked_postgresql_query()
    {
        await using PostgreSqlContainer postgreSql =
            CreatePostgreSql("organizations_claim_inspector_tests");
        await postgreSql.StartAsync();
        string connectionString = postgreSql.GetConnectionString();
        (Organization organization, OrganizationEnrollmentLink link) =
            await SeedEnrollmentAsync(connectionString, maximumClaims: 2);
        OrganizationEnrollmentClaim claim = CreatePendingClaim(
            organization.Id,
            link.Id,
            "subject-a");
        await using (OrganizationsDbContext seed = CreateDbContext(connectionString))
        {
            seed.EnrollmentClaims.Add(claim);
            await seed.SaveChangesAsync();
        }

        CountingCommandInterceptor commands = new();
        await using OrganizationsDbContext readerContext =
            CreateDbContext(connectionString, commands);
        OrganizationEnrollmentClaimInspector inspector = new(readerContext);

        ContractEnrollmentClaimDto? found = await inspector.FindAsync(
            organization.Id,
            link.Id,
            "subject-a");

        Assert.NotNull(found);
        Assert.Equal(claim.Id, found.ClaimId);
        Assert.Equal(ContractEnrollmentClaimStatus.Pending, found.Status);
        Assert.Equal(claim.DecisionExpiresAtUtc, found.DecisionExpiresAtUtc);
        Assert.Equal(1, commands.ReaderCommands);
        Assert.Empty(readerContext.ChangeTracker.Entries());
    }

    [DockerFact]
    public async Task Retention_removes_terminal_history_but_preserves_pending_join_requests()
    {
        await using PostgreSqlContainer postgreSql = CreatePostgreSql("organizations_retention_tests");
        await postgreSql.StartAsync();
        string connectionString = postgreSql.GetConnectionString();
        Organization organization = CreateOrganization("Retention House", "retention-house");
        OrganizationMembership membership = CreateMembership(organization.Id, "accepted-subject");
        DateTimeOffset oldCreatedAtUtc = Now.AddDays(-30);
        DateTimeOffset oldExpiryUtc = Now.AddDays(-20);
        DateTimeOffset recentExpiryUtc = Now.AddDays(-1);
        OrganizationInvitation oldInvitation = OrganizationInvitation.Create(
            Guid.NewGuid(), organization.Id, "owner", "old@example.test", new string('a', 64),
            oldExpiryUtc, "user:owner", Guid.NewGuid(), oldCreatedAtUtc).Value;
        OrganizationInvitation recentInvitation = OrganizationInvitation.Create(
            Guid.NewGuid(), organization.Id, "owner", "recent@example.test", new string('b', 64),
            recentExpiryUtc, "user:owner", Guid.NewGuid(), Now.AddDays(-2)).Value;
        Assert.True(oldInvitation.Expire(
            oldInvitation.Version, "system:lifecycle", Guid.NewGuid(), oldExpiryUtc).IsSuccess);
        Assert.True(recentInvitation.Expire(
            recentInvitation.Version, "system:lifecycle", Guid.NewGuid(), recentExpiryUtc).IsSuccess);
        OrganizationEnrollmentLink resolvedLink = OrganizationEnrollmentLink.Create(
            Guid.NewGuid(), organization.Id, "owner", new string('c', 64), oldExpiryUtc, 10,
            OrganizationEnrollmentApprovalMode.Automatic,
            "user:owner", Guid.NewGuid(), oldCreatedAtUtc).Value;
        Assert.True(resolvedLink.ReserveClaim(
            "user:accepted-subject", Guid.NewGuid(), oldCreatedAtUtc.AddHours(1)).IsSuccess);
        OrganizationEnrollmentClaim acceptedClaim = OrganizationEnrollmentClaim.Create(
            Guid.NewGuid(), organization.Id, resolvedLink.Id, "accepted-subject",
            OrganizationEnrollmentClaimState.Accepted, membership.Id,
            "user:accepted-subject", Guid.NewGuid(), oldCreatedAtUtc.AddHours(1)).Value;
        Assert.True(resolvedLink.Expire(
            resolvedLink.Version, "system:lifecycle", Guid.NewGuid(), oldExpiryUtc).IsSuccess);
        OrganizationEnrollmentLink pendingLink = OrganizationEnrollmentLink.Create(
            Guid.NewGuid(), organization.Id, "owner", new string('d', 64), oldExpiryUtc, 10,
            OrganizationEnrollmentApprovalMode.RequiresApproval,
            "user:owner", Guid.NewGuid(), oldCreatedAtUtc).Value;
        Assert.True(pendingLink.ReserveClaim(
            "user:pending-subject", Guid.NewGuid(), oldCreatedAtUtc.AddHours(1)).IsSuccess);
        OrganizationEnrollmentClaim pendingClaim = CreatePendingClaim(
            organization.Id, pendingLink.Id, "pending-subject", oldCreatedAtUtc.AddHours(1));

        await using (OrganizationsDbContext seed = CreateDbContext(connectionString))
        {
            await seed.Database.MigrateAsync();
            seed.AddRange(
                organization, membership, oldInvitation, recentInvitation,
                resolvedLink, acceptedClaim, pendingLink, pendingClaim);
            await seed.SaveChangesAsync();
        }

        ServiceCollection services = new();
        services.AddScoped(_ => CreateDbContext(connectionString));
        await using ServiceProvider provider = services.BuildServiceProvider();
        OrganizationsRetentionService retention = new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FixedClock(Now),
            Options.Create(new OrganizationsRetentionOptions
            {
                Enabled = true,
                InvitationHistoryDays = 7,
                EnrollmentHistoryDays = 7,
                BatchSize = 1,
                MaxBatchesPerCategoryPerCycle = 10,
                IntervalMinutes = 60
            }),
            NullLogger<OrganizationsRetentionService>.Instance);

        await retention.CleanupAsync(CancellationToken.None);

        await using OrganizationsDbContext verification = CreateDbContext(connectionString);
        Assert.DoesNotContain(await verification.Invitations.ToArrayAsync(), item => item.Id == oldInvitation.Id);
        Assert.Contains(await verification.Invitations.ToArrayAsync(), item => item.Id == recentInvitation.Id);
        Assert.DoesNotContain(await verification.EnrollmentClaims.ToArrayAsync(), item => item.Id == acceptedClaim.Id);
        Assert.DoesNotContain(await verification.EnrollmentLinks.ToArrayAsync(), item => item.Id == resolvedLink.Id);
        Assert.Contains(await verification.EnrollmentClaims.ToArrayAsync(), item => item.Id == pendingClaim.Id);
        Assert.Contains(await verification.EnrollmentLinks.ToArrayAsync(), item => item.Id == pendingLink.Id);
        Assert.Single(await verification.Memberships.ToArrayAsync());
        Assert.Single(await verification.Organizations.ToArrayAsync());
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
        string subjectId,
        DateTimeOffset? createdAtUtc = null)
    {
        DateTimeOffset created = createdAtUtc ?? Now.AddMinutes(1);
        return OrganizationEnrollmentClaim.Create(
            Guid.NewGuid(), organizationId, linkId, subjectId,
            OrganizationEnrollmentClaimState.Pending, null,
            $"user:{subjectId}", Guid.NewGuid(), created, created.AddDays(7)).Value;
    }

    private static Organization CreateOrganization(string name, string slug) => Organization.Create(
        Guid.NewGuid(), name, slug, "user:owner", Guid.NewGuid(), Now).Value;

    private static OrganizationMembership CreateMembership(Guid organizationId, string subjectId) =>
        OrganizationMembership.Create(
            Guid.NewGuid(), organizationId, subjectId, OrganizationMembershipRole.Member,
            "user:owner", Guid.NewGuid(), Now).Value;

    private static OrganizationsDbContext CreateDbContext(
        string connectionString,
        IInterceptor? interceptor = null)
    {
        DbContextOptionsBuilder<OrganizationsDbContext> builder =
            new DbContextOptionsBuilder<OrganizationsDbContext>()
                .UseNpgsql(connectionString, provider => provider
                    .MigrationsAssembly(OrganizationsMigrations.PostgreSqlAssembly)
                    .MigrationsHistoryTable(
                        OrganizationsMigrations.HistoryTable,
                        OrganizationsMigrations.Schema));
        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }

        return new OrganizationsDbContext(builder.Options);
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

    private sealed class CountingCommandInterceptor : DbCommandInterceptor
    {
        public int ReaderCommands { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            this.ReaderCommands++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : Gma.Framework.Runtime.Time.ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
