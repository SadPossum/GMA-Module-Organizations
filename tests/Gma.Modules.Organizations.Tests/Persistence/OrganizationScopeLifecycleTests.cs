namespace Gma.Modules.Organizations.Tests.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;
using DomainEnrollmentApprovalMode =
    Gma.Modules.Organizations.Domain.Enums.OrganizationEnrollmentApprovalMode;
using DomainMembershipRole =
    Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;

[Trait("Category", "Unit")]
public sealed class OrganizationScopeLifecycleTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Export_is_revision_bound_typed_paged_and_secret_free()
    {
        await using OrganizationsDbContext dbContext = CreateDbContext();
        Guid organizationId = Id(1);
        Organization organization = CreateOrganization(organizationId);
        OrganizationMembership firstMembership = CreateMembership(
            Id(2),
            organizationId,
            "subject-a",
            DomainMembershipRole.Owner);
        OrganizationMembership secondMembership = CreateMembership(
            Id(3),
            organizationId,
            "subject-b",
            DomainMembershipRole.Member);
        OrganizationInvitation invitation = CreateInvitation(
            Id(4),
            organizationId,
            Id(40),
            3);
        OrganizationEnrollmentLink link = CreateLink(
            Id(5),
            organizationId,
            Id(50),
            6);
        OrganizationEnrollmentClaim claim = CreateClaim(
            Id(6),
            organizationId,
            link.Id);
        dbContext.AddRange(
            organization,
            firstMembership,
            secondMembership,
            invitation,
            link,
            claim);
        await dbContext.SaveChangesAsync();
        OrganizationScopeLifecycleService service = CreateService(dbContext);

        OrganizationScopeSnapshot snapshot = await service.GetSnapshotAsync(
            organizationId,
            CancellationToken.None);
        Assert.Equal(OrganizationScopeStatus.Open, snapshot.Status);
        Assert.Equal(1, snapshot.Revision);

        OrganizationScopeExportPage firstMembershipPage = await service
            .ExportAsync(
                Request(
                    organizationId,
                    snapshot.Revision,
                    OrganizationScopeExportStore.Memberships,
                    pageSize: 1),
                CancellationToken.None);
        OrganizationScopeExportPage secondMembershipPage = await service
            .ExportAsync(
                Request(
                    organizationId,
                    snapshot.Revision,
                    OrganizationScopeExportStore.Memberships,
                    pageSize: 1,
                    firstMembershipPage.NextCursor),
                CancellationToken.None);

        Assert.True(firstMembershipPage.HasMore);
        Assert.Equal(
            firstMembership.Id,
            Assert.IsType<OrganizationScopeMembershipExportRecord>(
                Assert.Single(firstMembershipPage.Records)).MembershipId);
        Assert.False(secondMembershipPage.HasMore);
        Assert.Equal(
            secondMembership.Id,
            Assert.IsType<OrganizationScopeMembershipExportRecord>(
                Assert.Single(secondMembershipPage.Records)).MembershipId);

        Dictionary<OrganizationScopeExportStore, Type> expectedTypes = new()
        {
            [OrganizationScopeExportStore.Organization] =
                typeof(OrganizationScopeOrganizationExportRecord),
            [OrganizationScopeExportStore.Invitations] =
                typeof(OrganizationScopeInvitationExportRecord),
            [OrganizationScopeExportStore.EnrollmentLinks] =
                typeof(OrganizationScopeEnrollmentLinkExportRecord),
            [OrganizationScopeExportStore.EnrollmentClaims] =
                typeof(OrganizationScopeEnrollmentClaimExportRecord)
        };
        foreach ((OrganizationScopeExportStore store, Type expectedType) in
                 expectedTypes)
        {
            OrganizationScopeExportPage page = await service.ExportAsync(
                Request(organizationId, snapshot.Revision, store),
                CancellationToken.None);
            OrganizationScopeExportRecord record = Assert.Single(page.Records);

            Assert.Equal(OrganizationScopeExportStatus.Completed, page.Status);
            Assert.Equal(expectedType, record.GetType());
            Assert.DoesNotContain(
                record.GetType().GetProperties(),
                property => property.Name.Contains(
                    "Digest",
                    StringComparison.OrdinalIgnoreCase));
        }

        OrganizationScopeInvitationExportRecord invitationExport = Assert.IsType<
            OrganizationScopeInvitationExportRecord>(Assert.Single((await service.ExportAsync(
                Request(
                    organizationId,
                    snapshot.Revision,
                    OrganizationScopeExportStore.Invitations),
                CancellationToken.None)).Records));
        Assert.Equal(Id(40), invitationExport.ReplacesInvitationId);
        Assert.Equal(3, invitationExport.ReplacesInvitationVersion);
        OrganizationScopeEnrollmentLinkExportRecord enrollmentLinkExport = Assert.IsType<
            OrganizationScopeEnrollmentLinkExportRecord>(Assert.Single((await service.ExportAsync(
                Request(
                    organizationId,
                    snapshot.Revision,
                    OrganizationScopeExportStore.EnrollmentLinks),
                CancellationToken.None)).Records));
        Assert.Equal(Id(50), enrollmentLinkExport.ReplacesEnrollmentLinkId);
        Assert.Equal(6, enrollmentLinkExport.ReplacesEnrollmentLinkVersion);

        dbContext.Memberships.Add(CreateMembership(
            Id(7),
            organizationId,
            "subject-c",
            DomainMembershipRole.Member));
        await dbContext.SaveChangesAsync();
        OrganizationScopeExportPage stale = await service.ExportAsync(
            Request(
                organizationId,
                snapshot.Revision,
                OrganizationScopeExportStore.Organization),
            CancellationToken.None);
        Assert.Equal(OrganizationScopeExportStatus.Stale, stale.Status);
        Assert.Equal(2, stale.ScopeRevision);
    }

    [Fact]
    public async Task Destruction_resumes_across_owned_stores_and_exact_inbox_keys()
    {
        await using OrganizationsDbContext dbContext = CreateDbContext();
        Guid organizationId = Id(20);
        Organization organization = CreateOrganization(organizationId);
        OrganizationMembership membership = CreateMembership(
            Id(21),
            organizationId,
            "subject-a",
            DomainMembershipRole.Owner);
        OrganizationEnrollmentLink link = CreateLink(Id(22), organizationId);
        OrganizationEnrollmentClaim claim = CreateClaim(
            Id(23),
            organizationId,
            link.Id);
        OrganizationInvitation invitation = CreateInvitation(
            Id(24),
            organizationId);
        string scopeId = organizationId.ToString("D");
        dbContext.AddRange(
            organization,
            membership,
            link,
            claim,
            invitation,
            CreateOutbox(Id(25), scopeId),
            CreateInbox(Id(26), "handler-a", scopeId),
            CreateInbox(Id(26), "handler-b", scopeId));
        await dbContext.SaveChangesAsync();
        OrganizationScopeLifecycleService service = CreateService(dbContext);
        long selectedRevision = (await service.GetSnapshotAsync(
            organizationId,
            CancellationToken.None)).Revision;
        OrganizationScopeDestroyRequest request = new(
            Id(27),
            organizationId,
            selectedRevision,
            BatchSize: 2);

        OrganizationScopeDestroyResult result;
        int calls = 0;
        do
        {
            result = await service.DestroyBatchAsync(
                request,
                CancellationToken.None);
            calls++;
            Assert.InRange(calls, 1, 8);
        }
        while (result.Status == OrganizationScopeDestroyStatus.InProgress);

        Assert.Equal(OrganizationScopeDestroyStatus.Completed, result.Status);
        Assert.Equal(8, result.Receipt!.RemovedRecordCount);
        Assert.Equal(7, result.Receipt.CompletedBatchCount);
        Assert.Empty(await dbContext.Organizations.ToArrayAsync());
        Assert.Empty(await dbContext.Memberships.ToArrayAsync());
        Assert.Empty(await dbContext.Invitations.ToArrayAsync());
        Assert.Empty(await dbContext.EnrollmentLinks.ToArrayAsync());
        Assert.Empty(await dbContext.EnrollmentClaims.ToArrayAsync());
        Assert.Empty(await dbContext.InboxMessages.ToArrayAsync());
        Assert.Empty(await dbContext.OutboxMessages.ToArrayAsync());
        Assert.Empty(await dbContext.OrganizationScopeDestroyOperations
            .ToArrayAsync());
        Assert.True((await dbContext.OrganizationScopeStates.SingleAsync())
            .IsClosed);
        Assert.Single(await dbContext.OrganizationScopeDestroyReceipts
            .ToArrayAsync());

        OrganizationScopeDestroyResult replay = await service.DestroyBatchAsync(
            request,
            CancellationToken.None);
        OrganizationScopeDestroyResult conflict = await service.DestroyBatchAsync(
            request with { BatchSize = 3 },
            CancellationToken.None);
        Assert.Equal(OrganizationScopeDestroyStatus.Replayed, replay.Status);
        Assert.Equal(result.Receipt, replay.Receipt);
        Assert.Equal(OrganizationScopeDestroyStatus.Conflict, conflict.Status);

        dbContext.Memberships.Add(CreateMembership(
            Id(28),
            organizationId,
            "late-subject",
            DomainMembershipRole.Member));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Active_outbox_lease_returns_busy_before_scope_closure()
    {
        await using OrganizationsDbContext dbContext = CreateDbContext();
        Guid organizationId = Id(40);
        OutboxMessage outbox = CreateOutbox(
            Id(41),
            organizationId.ToString("D"));
        dbContext.AddRange(CreateOrganization(organizationId), outbox);
        await dbContext.SaveChangesAsync();
        outbox.MarkClaimed(
            "worker-a",
            Now,
            TimeSpan.FromMinutes(5));
        await dbContext.SaveChangesAsync();
        OrganizationScopeLifecycleService service = CreateService(dbContext);
        OrganizationScopeSnapshot selected = await service.GetSnapshotAsync(
            organizationId,
            CancellationToken.None);

        OrganizationScopeDestroyResult result = await service.DestroyBatchAsync(
            new OrganizationScopeDestroyRequest(
                Id(42),
                organizationId,
                selected.Revision,
                BatchSize: 10),
            CancellationToken.None);

        Assert.Equal(OrganizationScopeDestroyStatus.Busy, result.Status);
        Assert.False((await dbContext.OrganizationScopeStates.SingleAsync())
            .IsClosed);
        Assert.Empty(await dbContext.OrganizationScopeDestroyOperations
            .ToArrayAsync());
    }

    private static OrganizationScopeExportRequest Request(
        Guid organizationId,
        long revision,
        OrganizationScopeExportStore store,
        int pageSize = 20,
        string? afterCursor = null) =>
        new(organizationId, revision, store, afterCursor, pageSize);

    private static OrganizationScopeLifecycleService CreateService(
        OrganizationsDbContext dbContext) =>
        new(dbContext, new FixedClock());

    private static OrganizationsDbContext CreateDbContext()
    {
        DbContextOptions<OrganizationsDbContext> options =
            new DbContextOptionsBuilder<OrganizationsDbContext>()
                .UseInMemoryDatabase(
                    $"organization-scope-{Guid.NewGuid():N}",
                    new InMemoryDatabaseRoot())
                .ConfigureWarnings(warnings => warnings.Ignore(
                    InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        return new OrganizationsDbContext(options);
    }

    private static Organization CreateOrganization(Guid organizationId) =>
        Organization.Create(
            organizationId,
            "Scope House",
            $"scope-house-{organizationId:N}",
            "user:owner",
            Guid.NewGuid(),
            Now).Value;

    private static OrganizationMembership CreateMembership(
        Guid id,
        Guid organizationId,
        string subjectId,
        DomainMembershipRole role) =>
        OrganizationMembership.Create(
            id,
            organizationId,
            subjectId,
            role,
            "user:owner",
            Guid.NewGuid(),
            Now).Value;

    private static OrganizationInvitation CreateInvitation(
        Guid id,
        Guid organizationId,
        Guid? replacesInvitationId = null,
        long? replacesInvitationVersion = null) =>
        OrganizationInvitation.Create(
            id,
            organizationId,
            "subject-owner",
            "invitee@example.test",
            new string('a', 64),
            Now.AddDays(7),
            "user:owner",
            Guid.NewGuid(),
            Now,
            replacesInvitationId,
            replacesInvitationVersion).Value;

    private static OrganizationEnrollmentLink CreateLink(
        Guid id,
        Guid organizationId,
        Guid? replacesEnrollmentLinkId = null,
        long? replacesEnrollmentLinkVersion = null) =>
        OrganizationEnrollmentLink.Create(
            id,
            organizationId,
            "subject-owner",
            new string('b', 64),
            Now.AddDays(7),
            maximumClaims: 10,
            DomainEnrollmentApprovalMode.RequiresApproval,
            "user:owner",
            Guid.NewGuid(),
            Now,
            replacesEnrollmentLinkId,
            replacesEnrollmentLinkVersion).Value;

    private static OrganizationEnrollmentClaim CreateClaim(
        Guid id,
        Guid organizationId,
        Guid enrollmentLinkId) =>
        OrganizationEnrollmentClaim.Create(
            id,
            organizationId,
            enrollmentLinkId,
            "subject-claimant",
            OrganizationEnrollmentClaimState.Pending,
            membershipId: null,
            "user:owner",
            Guid.NewGuid(),
            Now,
            Now.AddDays(2)).Value;

    private static OutboxMessage CreateOutbox(Guid id, string scopeId) =>
        new(
            id,
            "gma.organizations.test.v1",
            "organization.test",
            version: 1,
            scopeId,
            Now,
            "{}",
            Now);

    private static InboxMessage CreateInbox(
        Guid id,
        string handler,
        string scopeId) =>
        InboxMessage.Create(
            id,
            handler,
            "gma.organizations.test.v1",
            "organization-test",
            version: 1,
            scopeId,
            Now,
            Now);

    private static Guid Id(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");

    private sealed class FixedClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
