namespace Gma.Modules.Organizations.Tests.Persistence;

using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Persistence;
using Gma.Modules.Organizations.Persistence.Access;
using Microsoft.EntityFrameworkCore;
using Xunit;
using OrganizationAccessContract =
    Gma.Modules.Organizations.Contracts.OrganizationAccessContract;
using OrganizationAccessDecision =
    Gma.Modules.Organizations.Contracts.OrganizationAccessDecision;
using ContractMembershipRole =
    Gma.Modules.Organizations.Contracts.OrganizationMembershipRole;
using ContractMembershipStatus =
    Gma.Modules.Organizations.Contracts.OrganizationMembershipStatus;
using ContractOrganizationStatus =
    Gma.Modules.Organizations.Contracts.OrganizationStatus;

[Trait("Category", "Unit")]
public sealed class OrganizationAccessDecisionReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Membership_reader_returns_the_current_exact_snapshot()
    {
        Guid organizationId = Guid.NewGuid();
        Organization organization = Organization.Create(
            organizationId,
            "Harbor House",
            "harbor-house",
            "user:owner",
            Guid.NewGuid(),
            Now).Value;
        OrganizationMembership membership = OrganizationMembership.Create(
            Guid.NewGuid(), organizationId, "member-a", OrganizationMembershipRole.Member,
            "user:owner", Guid.NewGuid(), Now).Value;
        Assert.True(membership.PromoteToOwner(
            membership.Version,
            "user:owner",
            Guid.NewGuid(),
            Now.AddMinutes(1)).IsSuccess);
        await using OrganizationsDbContext dbContext = CreateDbContext();
        dbContext.AddRange(organization, membership);
        await dbContext.SaveChangesAsync();
        OrganizationAccessDecisionReader reader = new(dbContext);

        Gma.Modules.Organizations.Contracts.OrganizationMembershipSnapshotDto? snapshot =
            await reader.FindAsync(
                organizationId,
                "member-a",
                CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal(ContractOrganizationStatus.Active, snapshot.OrganizationStatus);
        Assert.NotNull(snapshot.Membership);
        Assert.Equal(membership.Id, snapshot.Membership.MembershipId);
        Assert.Equal(ContractMembershipRole.Owner, snapshot.Membership.Role);
        Assert.Equal(ContractMembershipStatus.Active, snapshot.Membership.Status);
        Assert.Equal(membership.Version, snapshot.Membership.Version);
        Gma.Modules.Organizations.Contracts.OrganizationMembershipSnapshotDto? missingMembership =
            await reader.FindAsync(
            organizationId,
            "missing",
            CancellationToken.None);
        Assert.NotNull(missingMembership);
        Assert.Null(missingMembership.Membership);
        Assert.Null(await reader.FindAsync(
            Guid.NewGuid(),
            "missing",
            CancellationToken.None));

        Assert.True(organization.Suspend(
            organization.Version,
            "user:owner",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddMinutes(2)).IsSuccess);
        await dbContext.SaveChangesAsync();
        snapshot = await reader.FindAsync(
            organizationId,
            "member-a",
            CancellationToken.None);
        Assert.NotNull(snapshot);
        Assert.Equal(ContractOrganizationStatus.Suspended, snapshot.OrganizationStatus);
        Assert.Equal(ContractMembershipRole.Owner, snapshot.Membership?.Role);
    }

    [Fact]
    public async Task Reader_fails_closed_for_unavailable_organizations_and_memberships()
    {
        Guid organizationId = Guid.NewGuid();
        Organization organization = Organization.Create(
            organizationId, "Harbor House", "harbor-house", "user:owner", Guid.NewGuid(), Now).Value;
        OrganizationMembership membership = OrganizationMembership.Create(
            Guid.NewGuid(), organizationId, "member-a", OrganizationMembershipRole.Member,
            "user:owner", Guid.NewGuid(), Now).Value;
        await using OrganizationsDbContext dbContext = CreateDbContext();
        dbContext.AddRange(organization, membership);
        await dbContext.SaveChangesAsync();
        OrganizationAccessDecisionReader reader = new(dbContext);

        Assert.Equal(
            OrganizationAccessDecision.Allowed,
            await reader.ReadAsync(organizationId, "member-a", CancellationToken.None));
        Assert.Equal(
            OrganizationAccessDecision.MembershipNotFound,
            await reader.ReadAsync(organizationId, "missing", CancellationToken.None));

        Assert.True(membership.Suspend(
            membership.Version, "user:owner", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
        await dbContext.SaveChangesAsync();
        Assert.Equal(
            OrganizationAccessDecision.MembershipInactive,
            await reader.ReadAsync(organizationId, "member-a", CancellationToken.None));

        Assert.True(organization.Suspend(
            organization.Version, "user:owner", Guid.NewGuid(), Guid.NewGuid(), Now.AddMinutes(2)).IsSuccess);
        await dbContext.SaveChangesAsync();
        Assert.Equal(
            OrganizationAccessDecision.OrganizationInactive,
            await reader.ReadAsync(organizationId, "member-a", CancellationToken.None));
        Assert.Equal(
            OrganizationAccessDecision.OrganizationNotFound,
            await reader.ReadAsync(Guid.NewGuid(), "member-a", CancellationToken.None));
    }

    [Fact]
    public async Task Candidate_filter_returns_only_active_access_in_stable_distinct_order()
    {
        Guid organizationId = Guid.NewGuid();
        Organization organization = Organization.Create(
            organizationId, "Harbor House", "harbor-house", "user:owner", Guid.NewGuid(), Now).Value;
        OrganizationMembership first = OrganizationMembership.Create(
            Guid.NewGuid(), organizationId, "member-a", OrganizationMembershipRole.Member,
            "user:owner", Guid.NewGuid(), Now).Value;
        OrganizationMembership second = OrganizationMembership.Create(
            Guid.NewGuid(), organizationId, "member-b", OrganizationMembershipRole.Member,
            "user:owner", Guid.NewGuid(), Now).Value;
        OrganizationMembership suspended = OrganizationMembership.Create(
            Guid.NewGuid(), organizationId, "member-c", OrganizationMembershipRole.Member,
            "user:owner", Guid.NewGuid(), Now).Value;
        Assert.True(suspended.Suspend(
            suspended.Version, "user:owner", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
        await using OrganizationsDbContext dbContext = CreateDbContext();
        dbContext.AddRange(organization, first, second, suspended);
        await dbContext.SaveChangesAsync();
        OrganizationAccessDecisionReader reader = new(dbContext);

        IReadOnlyList<string> allowed = await reader.FilterAllowedAsync(
            organizationId,
            ["member-b", "member-c", "member-a", "member-b", "missing"],
            CancellationToken.None);

        Assert.Equal(["member-a", "member-b"], allowed);
        Assert.Empty(await reader.FilterAllowedAsync(
            organizationId,
            [],
            CancellationToken.None));

        Assert.True(organization.Suspend(
            organization.Version, "user:owner", Guid.NewGuid(), Guid.NewGuid(), Now.AddMinutes(2)).IsSuccess);
        await dbContext.SaveChangesAsync();
        Assert.Empty(await reader.FilterAllowedAsync(
            organizationId,
            ["member-a"],
            CancellationToken.None));
    }

    [Fact]
    public async Task Candidate_filter_rejects_invalid_or_unbounded_requests()
    {
        await using OrganizationsDbContext dbContext = CreateDbContext();
        OrganizationAccessDecisionReader reader = new(dbContext);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.FilterAllowedAsync(
            Guid.Empty,
            ["member-a"],
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => reader.FilterAllowedAsync(
            Guid.NewGuid(),
            null!,
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => reader.FilterAllowedAsync(
            Guid.NewGuid(),
            ["member a"],
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.FilterAllowedAsync(
            Guid.NewGuid(),
            Enumerable.Range(0, OrganizationAccessContract.MaximumCandidateCount + 1)
                .Select(index => $"member-{index}")
                .ToArray(),
            CancellationToken.None));
    }

    [Fact]
    public async Task Reader_rejects_invalid_requests_before_querying()
    {
        await using OrganizationsDbContext dbContext = CreateDbContext();
        OrganizationAccessDecisionReader reader = new(dbContext);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.ReadAsync(
            Guid.Empty,
            "member-a",
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => reader.ReadAsync(
            Guid.NewGuid(),
            "member a",
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => reader.ReadAsync(
            Guid.NewGuid(),
            null!,
            CancellationToken.None));
    }

    private static OrganizationsDbContext CreateDbContext()
    {
        DbContextOptions<OrganizationsDbContext> options =
            new DbContextOptionsBuilder<OrganizationsDbContext>()
                .UseInMemoryDatabase($"organizations-access-{Guid.NewGuid():N}")
                .Options;
        return new OrganizationsDbContext(options);
    }
}
