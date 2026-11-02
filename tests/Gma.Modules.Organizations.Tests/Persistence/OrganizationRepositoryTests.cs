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
    public async Task Every_directory_reports_bounded_lookahead_without_returning_the_extra_row()
    {
        await using OrganizationsDbContext dbContext = CreateDbContext();
        Organization first = CreateOrganization("First House", "first-house");
        Organization second = CreateOrganization("Second House", "second-house");
        OrganizationMembership firstMembership = CreateMembership(first.Id, "subject-a");
        OrganizationMembership secondMembership = CreateMembership(second.Id, "subject-a");
        OrganizationMembership colleague = CreateMembership(first.Id, "subject-b");
        OrganizationInvitation firstInvitation = CreateInvitation(first.Id, 'a');
        OrganizationInvitation secondInvitation = CreateInvitation(first.Id, 'b');
        OrganizationEnrollmentLink firstLink = CreateEnrollmentLink(first.Id, 'c');
        OrganizationEnrollmentLink secondLink = CreateEnrollmentLink(first.Id, 'd');
        OrganizationEnrollmentClaim firstClaim = CreatePendingClaim(
            first.Id, firstLink.Id, "claim-a", Now, Now.AddDays(1));
        OrganizationEnrollmentClaim secondClaim = CreatePendingClaim(
            first.Id, firstLink.Id, "claim-b", Now.AddMinutes(1), Now.AddDays(1));
        dbContext.AddRange(
            first,
            second,
            firstMembership,
            secondMembership,
            colleague,
            firstInvitation,
            secondInvitation,
            firstLink,
            secondLink,
            firstClaim,
            secondClaim);
        await dbContext.SaveChangesAsync();
        OrganizationRepository repository = new(dbContext);
        PageRequest firstPage = PageRequest.Normalize(1, 1);
        PageRequest secondPage = PageRequest.Normalize(2, 1);
        PageRequest emptyPage = PageRequest.Normalize(3, 1);

        var organizationsFirst = await repository.ListForSubjectAsync(
            "subject-a", firstPage, CancellationToken.None);
        var organizationsLast = await repository.ListForSubjectAsync(
            "subject-a", secondPage, CancellationToken.None);
        var organizationsEmpty = await repository.ListForSubjectAsync(
            "subject-a", emptyPage, CancellationToken.None);
        var catalogFirst = await repository.ListCatalogAsync(firstPage, CancellationToken.None);
        var catalogLast = await repository.ListCatalogAsync(secondPage, CancellationToken.None);
        var catalogEmpty = await repository.ListCatalogAsync(emptyPage, CancellationToken.None);
        var membersFirst = await repository.ListMembersAsync(first.Id, firstPage, CancellationToken.None);
        var membersLast = await repository.ListMembersAsync(first.Id, secondPage, CancellationToken.None);
        var membersEmpty = await repository.ListMembersAsync(first.Id, emptyPage, CancellationToken.None);
        var invitationsFirst = await repository.ListInvitationsAsync(
            first.Id, firstPage, Now, CancellationToken.None);
        var invitationsLast = await repository.ListInvitationsAsync(
            first.Id, secondPage, Now, CancellationToken.None);
        var invitationsEmpty = await repository.ListInvitationsAsync(
            first.Id, emptyPage, Now, CancellationToken.None);
        var linksFirst = await repository.ListEnrollmentLinksAsync(
            first.Id, firstPage, Now, CancellationToken.None);
        var linksLast = await repository.ListEnrollmentLinksAsync(
            first.Id, secondPage, Now, CancellationToken.None);
        var linksEmpty = await repository.ListEnrollmentLinksAsync(
            first.Id, emptyPage, Now, CancellationToken.None);
        var claimsFirst = await repository.ListPendingJoinRequestsAsync(
            first.Id, firstPage, Now, CancellationToken.None);
        var claimsLast = await repository.ListPendingJoinRequestsAsync(
            first.Id, secondPage, Now, CancellationToken.None);
        var claimsEmpty = await repository.ListPendingJoinRequestsAsync(
            first.Id, emptyPage, Now, CancellationToken.None);

        AssertContinuation(
            organizationsFirst.Items, organizationsFirst.HasMore,
            organizationsLast.Items, organizationsLast.HasMore,
            organizationsEmpty.Items, organizationsEmpty.HasMore);
        AssertContinuation(
            catalogFirst.Items, catalogFirst.HasMore,
            catalogLast.Items, catalogLast.HasMore,
            catalogEmpty.Items, catalogEmpty.HasMore);
        AssertContinuation(
            membersFirst.Items, membersFirst.HasMore,
            membersLast.Items, membersLast.HasMore,
            membersEmpty.Items, membersEmpty.HasMore);
        AssertContinuation(
            invitationsFirst.Items, invitationsFirst.HasMore,
            invitationsLast.Items, invitationsLast.HasMore,
            invitationsEmpty.Items, invitationsEmpty.HasMore);
        AssertContinuation(
            linksFirst.Items, linksFirst.HasMore,
            linksLast.Items, linksLast.HasMore,
            linksEmpty.Items, linksEmpty.HasMore);
        AssertContinuation(
            claimsFirst.Items, claimsFirst.HasMore,
            claimsLast.Items, claimsLast.HasMore,
            claimsEmpty.Items, claimsEmpty.HasMore);
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
    public void Membership_model_has_organization_id_keyset_export_index()
    {
        using OrganizationsDbContext dbContext = CreateDbContext();

        var entity = dbContext.Model.FindEntityType(
            typeof(OrganizationMembership));
        var index = Assert.Single(entity!.GetIndexes(), item =>
            item.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(OrganizationMembership.OrganizationId),
                 nameof(OrganizationMembership.Id)]));

        Assert.False(index.IsUnique);
    }

    [Fact]
    public void Organization_creation_fingerprint_is_optional_fixed_length_state()
    {
        using OrganizationsDbContext dbContext = CreateDbContext();

        var entity = dbContext.Model.FindEntityType(typeof(Organization));
        var property = entity!.FindProperty(
            nameof(Organization.CreationRequestFingerprint));

        Assert.NotNull(property);
        Assert.True(property.IsNullable);
        Assert.Equal(64, property.GetMaxLength());
        Assert.True(property.IsFixedLength());
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
    public void Enrollment_claim_model_has_a_join_subject_lookup_index()
    {
        using OrganizationsDbContext dbContext = CreateDbContext();

        var entity = dbContext.Model.FindEntityType(typeof(OrganizationEnrollmentClaim));
        Assert.Single(entity!.GetIndexes(), item =>
            item.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(OrganizationEnrollmentClaim.OrganizationId),
                 nameof(OrganizationEnrollmentClaim.SubjectId),
                 nameof(OrganizationEnrollmentClaim.Status),
                 nameof(OrganizationEnrollmentClaim.DecisionExpiresAtUtc)]));
    }

    [Fact]
    public async Task Join_subject_lookup_ignores_other_subjects_and_overdue_requests()
    {
        await using OrganizationsDbContext dbContext = CreateDbContext();
        Organization organization = CreateOrganization("Join House", "join-house");
        dbContext.AddRange(
            organization,
            CreatePendingClaim(
                organization.Id,
                Guid.NewGuid(),
                "current",
                Now.AddHours(-1),
                Now.AddHours(1)),
            CreatePendingClaim(
                organization.Id,
                Guid.NewGuid(),
                "overdue",
                Now.AddDays(-8),
                Now.AddMinutes(-1)));
        await dbContext.SaveChangesAsync();
        OrganizationRepository repository = new(dbContext);

        Assert.True(await repository.HasCurrentPendingEnrollmentClaimAsync(
            organization.Id,
            "current",
            Now,
            CancellationToken.None));
        Assert.False(await repository.HasCurrentPendingEnrollmentClaimAsync(
            organization.Id,
            "overdue",
            Now,
            CancellationToken.None));
        Assert.False(await repository.HasCurrentPendingEnrollmentClaimAsync(
            organization.Id,
            "missing",
            Now,
            CancellationToken.None));
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

    private static OrganizationInvitation CreateInvitation(Guid organizationId, char digestCharacter) =>
        OrganizationInvitation.Create(
            Guid.NewGuid(), organizationId, "owner", null, new string(digestCharacter, 64),
            Now.AddDays(1), "user:owner", Guid.NewGuid(), Now).Value;

    private static OrganizationEnrollmentLink CreateEnrollmentLink(Guid organizationId, char digestCharacter) =>
        OrganizationEnrollmentLink.Create(
            Guid.NewGuid(), organizationId, "owner", new string(digestCharacter, 64),
            Now.AddDays(1), 10, OrganizationEnrollmentApprovalMode.RequiresApproval,
            "user:owner", Guid.NewGuid(), Now).Value;

    private static void AssertContinuation<T>(
        IReadOnlyList<T> firstItems,
        bool firstHasMore,
        IReadOnlyList<T> lastItems,
        bool lastHasMore,
        IReadOnlyList<T> emptyItems,
        bool emptyHasMore)
    {
        Assert.Single(firstItems);
        Assert.True(firstHasMore);
        Assert.Single(lastItems);
        Assert.False(lastHasMore);
        Assert.Empty(emptyItems);
        Assert.False(emptyHasMore);
    }

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
