namespace Gma.Modules.Organizations.Tests.Persistence;

using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Persistence;
using Gma.Modules.Organizations.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Membership_discovery_is_global_and_returns_only_the_requested_subject()
    {
        await using OrganizationsDbContext dbContext = CreateDbContext();
        Organization first = CreateOrganization("First House", "first-house");
        Organization second = CreateOrganization("Second House", "second-house");
        dbContext.Organizations.AddRange(first, second);
        dbContext.Memberships.AddRange(
            CreateMembership(first.Id, "subject-a"),
            CreateMembership(second.Id, "subject-a"),
            CreateMembership(second.Id, "subject-b"));
        await dbContext.SaveChangesAsync();
        OrganizationRepository repository = new(dbContext);

        var result = await repository.ListForSubjectAsync(
            "subject-a", page: 1, pageSize: 25, CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item => Assert.Equal("subject-a", item.Membership.SubjectId));
    }

    [Fact]
    public void Membership_model_has_one_unique_organization_subject_index()
    {
        using OrganizationsDbContext dbContext = CreateDbContext();

        var entity = dbContext.Model.FindEntityType(typeof(OrganizationMembership));
        var index = Assert.Single(entity!.GetIndexes(), item =>
            item.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(OrganizationMembership.OrganizationId), nameof(OrganizationMembership.SubjectId)]));
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void Enrollment_claim_model_has_one_unique_link_subject_index()
    {
        using OrganizationsDbContext dbContext = CreateDbContext();

        var entity = dbContext.Model.FindEntityType(typeof(OrganizationEnrollmentClaim));
        var index = Assert.Single(entity!.GetIndexes(), item =>
            item.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(OrganizationEnrollmentClaim.EnrollmentLinkId), nameof(OrganizationEnrollmentClaim.SubjectId)]));
        Assert.True(index.IsUnique);
    }

    private static OrganizationsDbContext CreateDbContext()
    {
        DbContextOptions<OrganizationsDbContext> options =
            new DbContextOptionsBuilder<OrganizationsDbContext>()
                .UseInMemoryDatabase($"organizations-{Guid.NewGuid():N}")
                .Options;
        return new OrganizationsDbContext(options);
    }

    private static Organization CreateOrganization(string name, string slug) => Organization.Create(
        Guid.NewGuid(), name, slug, "user:owner", Guid.NewGuid(), Now).Value;

    private static OrganizationMembership CreateMembership(Guid organizationId, string subjectId) =>
        OrganizationMembership.Create(
            Guid.NewGuid(), organizationId, subjectId, OrganizationMembershipRole.Member,
            "user:owner", Guid.NewGuid(), Now).Value;
}
