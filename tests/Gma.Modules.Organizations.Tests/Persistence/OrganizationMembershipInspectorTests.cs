namespace Gma.Modules.Organizations.Tests.Persistence;

using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Entities;
using Gma.Modules.Organizations.Persistence;
using Gma.Modules.Organizations.Persistence.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using DomainMembershipRole =
    Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;
using ScopeCloseTransition =
    Gma.Modules.Organizations.Domain.Entities.OrganizationScopeCloseTransition;

[Trait("Category", "Unit")]
public sealed class OrganizationMembershipInspectorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Snapshot_contract_exposes_only_minimal_reconciliation_state()
    {
        string[] expectedProperties =
        [
            nameof(OrganizationMembershipSnapshot.OrganizationId),
            nameof(OrganizationMembershipSnapshot.MembershipId),
            nameof(OrganizationMembershipSnapshot.OrganizationStatus),
            nameof(OrganizationMembershipSnapshot.ScopeStatus),
            nameof(OrganizationMembershipSnapshot.ScopeRevision),
            nameof(OrganizationMembershipSnapshot.Role),
            nameof(OrganizationMembershipSnapshot.MembershipStatus),
            nameof(OrganizationMembershipSnapshot.MembershipVersion)
        ];

        Assert.Equal(
            expectedProperties.Order(StringComparer.Ordinal),
            typeof(OrganizationMembershipSnapshot)
                .GetProperties()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Inspector_returns_one_exact_snapshot_without_tracking_it()
    {
        Guid organizationId = Guid.NewGuid();
        Guid membershipId = Guid.NewGuid();
        await using OrganizationsDbContext dbContext = CreateDbContext();
        dbContext.AddRange(
            CreateOrganization(organizationId),
            CreateMembership(
                membershipId,
                organizationId,
                "Subject-A",
                DomainMembershipRole.Owner));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        OrganizationMembershipInspector inspector = new(dbContext);

        OrganizationMembershipSnapshot? found = await inspector.FindAsync(
            organizationId,
            membershipId,
            " Subject-A ");

        Assert.NotNull(found);
        Assert.Equal(organizationId, found.OrganizationId);
        Assert.Equal(membershipId, found.MembershipId);
        Assert.Equal(OrganizationStatus.Active, found.OrganizationStatus);
        Assert.Equal(OrganizationScopeStatus.Open, found.ScopeStatus);
        Assert.Equal(1, found.ScopeRevision);
        Assert.Equal(OrganizationMembershipRole.Owner, found.Role);
        Assert.Equal(
            OrganizationMembershipStatus.Active,
            found.MembershipStatus);
        Assert.Equal(1, found.MembershipVersion);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Inspector_does_not_cross_exact_keys_or_subject_case()
    {
        Guid firstOrganizationId = Guid.NewGuid();
        Guid secondOrganizationId = Guid.NewGuid();
        Guid firstMembershipId = Guid.NewGuid();
        await using OrganizationsDbContext dbContext = CreateDbContext();
        dbContext.AddRange(
            CreateOrganization(firstOrganizationId),
            CreateOrganization(secondOrganizationId),
            CreateMembership(
                firstMembershipId,
                firstOrganizationId,
                "Subject-A",
                DomainMembershipRole.Owner),
            CreateMembership(
                Guid.NewGuid(),
                secondOrganizationId,
                "Subject-A",
                DomainMembershipRole.Owner));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        OrganizationMembershipInspector inspector = new(dbContext);

        Assert.Null(await inspector.FindAsync(
            secondOrganizationId,
            firstMembershipId,
            "Subject-A"));
        Assert.Null(await inspector.FindAsync(
            firstOrganizationId,
            Guid.NewGuid(),
            "Subject-A"));
        Assert.Null(await inspector.FindAsync(
            firstOrganizationId,
            firstMembershipId,
            "subject-a"));
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Inspector_returns_current_lifecycle_and_revision_matrix()
    {
        Guid organizationId = Guid.NewGuid();
        Guid membershipId = Guid.NewGuid();
        await using OrganizationsDbContext dbContext = CreateDbContext();
        dbContext.AddRange(
            CreateOrganization(organizationId),
            CreateMembership(
                membershipId,
                organizationId,
                "subject-a",
                DomainMembershipRole.Member));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        OrganizationMembershipInspector inspector = new(dbContext);

        AssertSnapshot(
            await inspector.FindAsync(
                organizationId,
                membershipId,
                "subject-a"),
            OrganizationStatus.Active,
            OrganizationScopeStatus.Open,
            scopeRevision: 1,
            OrganizationMembershipRole.Member,
            OrganizationMembershipStatus.Active,
            membershipVersion: 1);

        OrganizationMembership membership = await dbContext.Memberships.SingleAsync();
        Assert.True(membership.PromoteToOwner(
            membership.Version,
            "subject-owner",
            Guid.NewGuid(),
            Now.AddMinutes(1)).IsSuccess);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        AssertSnapshot(
            await inspector.FindAsync(
                organizationId,
                membershipId,
                "subject-a"),
            OrganizationStatus.Active,
            OrganizationScopeStatus.Open,
            scopeRevision: 2,
            OrganizationMembershipRole.Owner,
            OrganizationMembershipStatus.Active,
            membershipVersion: 2);

        membership = await dbContext.Memberships.SingleAsync();
        Organization organization = await dbContext.Organizations.SingleAsync();
        Assert.True(membership.Suspend(
            membership.Version,
            "subject-owner",
            Guid.NewGuid(),
            Now.AddMinutes(2)).IsSuccess);
        Assert.True(organization.Suspend(
            organization.Version,
            "subject-owner",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddMinutes(2)).IsSuccess);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        AssertSnapshot(
            await inspector.FindAsync(
                organizationId,
                membershipId,
                "subject-a"),
            OrganizationStatus.Suspended,
            OrganizationScopeStatus.Open,
            scopeRevision: 3,
            OrganizationMembershipRole.Owner,
            OrganizationMembershipStatus.Suspended,
            membershipVersion: 3);

        membership = await dbContext.Memberships.SingleAsync();
        organization = await dbContext.Organizations.SingleAsync();
        Assert.True(membership.Remove(
            membership.Version,
            "subject-owner",
            Guid.NewGuid(),
            Now.AddMinutes(3)).IsSuccess);
        Assert.True(organization.Archive(
            organization.Version,
            "subject-owner",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddMinutes(3)).IsSuccess);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        AssertSnapshot(
            await inspector.FindAsync(
                organizationId,
                membershipId,
                "subject-a"),
            OrganizationStatus.Archived,
            OrganizationScopeStatus.Open,
            scopeRevision: 4,
            OrganizationMembershipRole.Owner,
            OrganizationMembershipStatus.Removed,
            membershipVersion: 4);

        OrganizationScopeState scopeState = await dbContext
            .OrganizationScopeStates
            .SingleAsync();
        Assert.Equal(
            ScopeCloseTransition.Completed,
            scopeState.Close(
                Guid.NewGuid(),
                new string('a', 64),
                Now.AddMinutes(4)));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        AssertSnapshot(
            await inspector.FindAsync(
                organizationId,
                membershipId,
                "subject-a"),
            OrganizationStatus.Archived,
            OrganizationScopeStatus.Closed,
            scopeRevision: 5,
            OrganizationMembershipRole.Owner,
            OrganizationMembershipStatus.Removed,
            membershipVersion: 4);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Inspector_treats_legacy_missing_scope_state_as_open_revision_zero()
    {
        Guid organizationId = Guid.NewGuid();
        Guid membershipId = Guid.NewGuid();
        await using OrganizationsDbContext dbContext = CreateDbContext();
        dbContext.AddRange(
            CreateOrganization(organizationId),
            CreateMembership(
                membershipId,
                organizationId,
                "subject-a",
                DomainMembershipRole.Member));
        await dbContext.SaveChangesAsync();
        dbContext.OrganizationScopeStates.Remove(
            await dbContext.OrganizationScopeStates.SingleAsync());
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        OrganizationMembershipInspector inspector = new(dbContext);

        OrganizationMembershipSnapshot? found = await inspector.FindAsync(
            organizationId,
            membershipId,
            "subject-a");

        Assert.NotNull(found);
        Assert.Equal(OrganizationScopeStatus.Open, found.ScopeStatus);
        Assert.Equal(0, found.ScopeRevision);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Inspector_rejects_invalid_keys_before_using_persistence()
    {
        OrganizationsDbContext dbContext = CreateDbContext();
        await dbContext.DisposeAsync();
        OrganizationMembershipInspector inspector = new(dbContext);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            inspector.FindAsync(Guid.Empty, Guid.NewGuid(), "subject-a"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            inspector.FindAsync(Guid.NewGuid(), Guid.Empty, "subject-a"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            inspector.FindAsync(Guid.NewGuid(), Guid.NewGuid(), "subject a"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            inspector.FindAsync(Guid.NewGuid(), Guid.NewGuid(), null!));
    }

    [Fact]
    public async Task Inspector_propagates_provider_failures_and_cancellation()
    {
        OrganizationsDbContext disposedContext = CreateDbContext();
        OrganizationMembershipInspector disposedInspector = new(disposedContext);
        await disposedContext.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            disposedInspector.FindAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "subject-a"));

        await using OrganizationsDbContext canceledContext = CreateDbContext();
        OrganizationMembershipInspector canceledInspector = new(canceledContext);
        CancellationToken cancellationToken = new(canceled: true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            canceledInspector.FindAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "subject-a",
                cancellationToken));
    }

    [Fact]
    public void Persistence_composition_registers_a_scoped_membership_inspector()
    {
        HostApplicationBuilder builder = CreateBuilder();

        builder.AddOrganizationsPersistence();

        ServiceDescriptor registration = Assert.Single(
            builder.Services,
            descriptor => descriptor.ServiceType ==
                typeof(IOrganizationMembershipInspector));
        Assert.Equal(
            typeof(OrganizationMembershipInspector),
            registration.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, registration.Lifetime);
    }

    [Fact]
    public void Persistence_composition_preserves_a_host_membership_inspector_override()
    {
        HostApplicationBuilder builder = CreateBuilder();
        builder.Services.AddScoped<
            IOrganizationMembershipInspector,
            HostMembershipInspector>();

        builder.AddOrganizationsPersistence();

        ServiceDescriptor registration = Assert.Single(
            builder.Services,
            descriptor => descriptor.ServiceType ==
                typeof(IOrganizationMembershipInspector));
        Assert.Equal(typeof(HostMembershipInspector), registration.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, registration.Lifetime);
    }

    private static void AssertSnapshot(
        OrganizationMembershipSnapshot? snapshot,
        OrganizationStatus organizationStatus,
        OrganizationScopeStatus scopeStatus,
        long scopeRevision,
        OrganizationMembershipRole role,
        OrganizationMembershipStatus membershipStatus,
        long membershipVersion)
    {
        Assert.NotNull(snapshot);
        Assert.Equal(organizationStatus, snapshot.OrganizationStatus);
        Assert.Equal(scopeStatus, snapshot.ScopeStatus);
        Assert.Equal(scopeRevision, snapshot.ScopeRevision);
        Assert.Equal(role, snapshot.Role);
        Assert.Equal(membershipStatus, snapshot.MembershipStatus);
        Assert.Equal(membershipVersion, snapshot.MembershipVersion);
    }

    private static HostApplicationBuilder CreateBuilder()
    {
        HostApplicationBuilder builder = new(new HostApplicationBuilderSettings
        {
            DisableDefaults = true
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SqlServer"] =
                "Server=localhost;Database=organizations-tests;Integrated Security=true;TrustServerCertificate=true"
        });
        return builder;
    }

    private static Organization CreateOrganization(Guid organizationId) =>
        Organization.Create(
            organizationId,
            "Harbor House",
            $"harbor-{organizationId:N}",
            "subject-owner",
            Guid.NewGuid(),
            Now).Value;

    private static OrganizationMembership CreateMembership(
        Guid membershipId,
        Guid organizationId,
        string subjectId,
        DomainMembershipRole role) => OrganizationMembership.Create(
            membershipId,
            organizationId,
            subjectId,
            role,
            "subject-owner",
            Guid.NewGuid(),
            Now).Value;

    private static OrganizationsDbContext CreateDbContext()
    {
        DbContextOptions<OrganizationsDbContext> options =
            new DbContextOptionsBuilder<OrganizationsDbContext>()
                .UseInMemoryDatabase($"organizations-membership-inspector-{Guid.NewGuid():N}")
                .Options;
        return new OrganizationsDbContext(options);
    }

    private sealed class HostMembershipInspector : IOrganizationMembershipInspector
    {
        public Task<OrganizationMembershipSnapshot?> FindAsync(
            Guid organizationId,
            Guid membershipId,
            string subjectId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<OrganizationMembershipSnapshot?>(null);
    }
}
