namespace Gma.Modules.Organizations.Tests.Persistence;

using Gma.Framework.Pagination;
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
            "subject-a", PageRequest.Normalize(1, 25), CancellationToken.None);

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

    [Fact]
    public void Enrollment_claim_model_has_a_bounded_due_query_index()
    {
        using OrganizationsDbContext dbContext = CreateDbContext();

        var entity = dbContext.Model.FindEntityType(typeof(OrganizationEnrollmentClaim));
        Assert.Single(entity!.GetIndexes(), item =>
            item.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(OrganizationEnrollmentClaim.Status),
                 nameof(OrganizationEnrollmentClaim.DecisionExpiresAtUtc)]));
    }

    [Fact]
    public async Task Join_request_queries_hide_overdue_pending_claims_during_worker_lag()
    {
        await using OrganizationsDbContext dbContext = CreateDbContext();
        Organization organization = CreateOrganization("Review House", "review-house");
        Guid linkId = Guid.NewGuid();
        OrganizationEnrollmentClaim overdue = CreatePendingClaim(
            organization.Id, linkId, "overdue", Now.AddDays(-8), Now.AddDays(-1));
        OrganizationEnrollmentClaim reviewable = CreatePendingClaim(
            organization.Id, linkId, "reviewable", Now.AddDays(-1), Now.AddDays(6));
        dbContext.AddRange(organization, overdue, reviewable);
        await dbContext.SaveChangesAsync();
        OrganizationRepository repository = new(dbContext);

        var result = await repository.ListPendingJoinRequestsAsync(
            organization.Id, PageRequest.Normalize(1, 25), Now, CancellationToken.None);

        Assert.Equal(reviewable.Id, Assert.Single(result.Items).ClaimId);
        Assert.Equal(reviewable.DecisionExpiresAtUtc, result.Items[0].DecisionExpiresAtUtc);
    }

    [Fact]
    public async Task Lifecycle_queries_select_only_due_nonterminal_aggregates()
    {
        await using OrganizationsDbContext dbContext = CreateDbContext();
        Organization organization = CreateOrganization("Expiry House", "expiry-house");
        OrganizationInvitation dueInvitation = OrganizationInvitation.Create(
            Guid.NewGuid(), organization.Id, "owner", null, new string('a', 64),
            Now.AddMinutes(-1), "user:owner", Guid.NewGuid(), Now.AddDays(-1)).Value;
        OrganizationInvitation futureInvitation = OrganizationInvitation.Create(
            Guid.NewGuid(), organization.Id, "owner", null, new string('b', 64),
            Now.AddMinutes(1), "user:owner", Guid.NewGuid(), Now.AddDays(-1)).Value;
        OrganizationEnrollmentLink dueLink = OrganizationEnrollmentLink.Create(
            Guid.NewGuid(), organization.Id, "owner", new string('c', 64),
            Now.AddMinutes(-1), 10, OrganizationEnrollmentApprovalMode.RequiresApproval,
            "user:owner", Guid.NewGuid(), Now.AddDays(-1)).Value;
        OrganizationEnrollmentLink futureLink = OrganizationEnrollmentLink.Create(
            Guid.NewGuid(), organization.Id, "owner", new string('d', 64),
            Now.AddMinutes(1), 10, OrganizationEnrollmentApprovalMode.RequiresApproval,
            "user:owner", Guid.NewGuid(), Now.AddDays(-1)).Value;
        OrganizationEnrollmentClaim dueClaim = CreatePendingClaim(
            organization.Id, dueLink.Id, "due", Now.AddDays(-1), Now.AddMinutes(-1));
        OrganizationEnrollmentClaim futureClaim = CreatePendingClaim(
            organization.Id, futureLink.Id, "future", Now.AddDays(-1), Now.AddMinutes(1));
        dbContext.AddRange(
            organization, dueInvitation, futureInvitation, dueLink, futureLink, dueClaim, futureClaim);
        await dbContext.SaveChangesAsync();
        OrganizationLifecycleRepository repository = new(dbContext);

        Assert.Equal(
            dueInvitation.Id,
            Assert.Single(await repository.ListDueInvitationsAsync(Now, 10, CancellationToken.None)).Id);
        Assert.Equal(
            dueClaim.Id,
            Assert.Single(await repository.ListDueEnrollmentClaimsAsync(
                Now, 10, CancellationToken.None)).Claim.Id);
        Assert.Equal(
            dueLink.Id,
            Assert.Single(await repository.ListDueEnrollmentLinksAsync(Now, 10, CancellationToken.None)).Id);
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

    private static OrganizationEnrollmentClaim CreatePendingClaim(
        Guid organizationId,
        Guid linkId,
        string subjectId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc) => OrganizationEnrollmentClaim.Create(
            Guid.NewGuid(), organizationId, linkId, subjectId,
            OrganizationEnrollmentClaimState.Pending, null,
            $"user:{subjectId}", Guid.NewGuid(), createdAtUtc, expiresAtUtc).Value;
}
