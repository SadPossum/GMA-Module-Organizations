namespace Gma.Modules.Organizations.Tests.Persistence;

using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Persistence;
using Gma.Modules.Organizations.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationEnrollmentClaimInspectorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 28, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Inspector_returns_one_exact_authoritative_claim_without_tracking_it()
    {
        Guid organizationId = Guid.NewGuid();
        Guid enrollmentLinkId = Guid.NewGuid();
        Guid membershipId = Guid.NewGuid();
        OrganizationEnrollmentClaim claim = OrganizationEnrollmentClaim.Create(
            Guid.NewGuid(),
            organizationId,
            enrollmentLinkId,
            "subject-a",
            OrganizationEnrollmentClaimState.Pending,
            null,
            "subject-a",
            Guid.NewGuid(),
            Now,
            Now.AddDays(7)).Value;
        Assert.True(claim.Approve(
            membershipId,
            claim.Version,
            "subject:owner",
            Guid.NewGuid(),
            Now.AddMinutes(1)).IsSuccess);

        await using OrganizationsDbContext dbContext = CreateDbContext();
        dbContext.EnrollmentClaims.Add(claim);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        OrganizationEnrollmentClaimInspector inspector = new(dbContext);

        OrganizationEnrollmentClaimDto? found = await inspector.FindAsync(
            organizationId,
            enrollmentLinkId,
            " subject-a ");

        Assert.NotNull(found);
        Assert.Equal(claim.Id, found.ClaimId);
        Assert.Equal(organizationId, found.OrganizationId);
        Assert.Equal(enrollmentLinkId, found.EnrollmentLinkId);
        Assert.Equal("subject-a", found.SubjectId);
        Assert.Equal(OrganizationEnrollmentClaimStatus.Accepted, found.Status);
        Assert.Equal(membershipId, found.MembershipId);
        Assert.Equal(claim.Version, found.Version);
        Assert.Equal(Now, found.CreatedAtUtc);
        Assert.Equal(Now.AddMinutes(1), found.LastChangedAtUtc);
        Assert.Equal(Now.AddDays(7), found.DecisionExpiresAtUtc);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Inspector_does_not_cross_organization_link_or_subject_keys()
    {
        Guid organizationId = Guid.NewGuid();
        Guid enrollmentLinkId = Guid.NewGuid();
        OrganizationEnrollmentClaim claim = OrganizationEnrollmentClaim.Create(
            Guid.NewGuid(),
            organizationId,
            enrollmentLinkId,
            "subject-a",
            OrganizationEnrollmentClaimState.Pending,
            null,
            "subject-a",
            Guid.NewGuid(),
            Now,
            Now.AddDays(7)).Value;
        await using OrganizationsDbContext dbContext = CreateDbContext();
        dbContext.EnrollmentClaims.Add(claim);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        OrganizationEnrollmentClaimInspector inspector = new(dbContext);

        Assert.Null(await inspector.FindAsync(
            Guid.NewGuid(), enrollmentLinkId, "subject-a"));
        Assert.Null(await inspector.FindAsync(
            organizationId, Guid.NewGuid(), "subject-a"));
        Assert.Null(await inspector.FindAsync(
            organizationId, enrollmentLinkId, "subject-b"));
    }

    [Fact]
    public async Task Inspector_rejects_invalid_keys_before_querying()
    {
        await using OrganizationsDbContext dbContext = CreateDbContext();
        OrganizationEnrollmentClaimInspector inspector = new(dbContext);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            inspector.FindAsync(Guid.Empty, Guid.NewGuid(), "subject-a"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            inspector.FindAsync(Guid.NewGuid(), Guid.Empty, "subject-a"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            inspector.FindAsync(Guid.NewGuid(), Guid.NewGuid(), "subject a"));
    }

    [Fact]
    public void Persistence_composition_registers_the_claim_inspector()
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

        builder.AddOrganizationsPersistence();

        Assert.Contains(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(IOrganizationEnrollmentClaimInspector) &&
            descriptor.ImplementationType == typeof(OrganizationEnrollmentClaimInspector) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    private static OrganizationsDbContext CreateDbContext()
    {
        DbContextOptions<OrganizationsDbContext> options =
            new DbContextOptionsBuilder<OrganizationsDbContext>()
                .UseInMemoryDatabase($"organizations-claim-inspector-{Guid.NewGuid():N}")
                .Options;
        return new OrganizationsDbContext(options);
    }
}
