namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Framework.Cqrs;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class CreateOrganizationHandlerTests
{
    [Fact]
    public async Task Self_service_creation_is_disabled_by_default()
    {
        TestRepository repository = new();
        using ServiceProvider services = CreateServices(repository, enabled: false);
        var handler = services.GetRequiredService<
            ICommandHandler<CreateOrganizationCommand, OrganizationMembershipSummaryDto>>();

        var result = await handler.HandleAsync(
            new CreateOrganizationCommand("Harbor House", "harbor-house", "subject-a", "user:subject-a"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(OrganizationApplicationErrors.SelfServiceCreationDisabled, result.Error);
        Assert.Empty(repository.Organizations);
    }

    [Fact]
    public async Task Enabled_creation_stages_organization_and_first_owner_atomically()
    {
        TestRepository repository = new();
        using ServiceProvider services = CreateServices(repository, enabled: true);
        var handler = services.GetRequiredService<
            ICommandHandler<CreateOrganizationCommand, OrganizationMembershipSummaryDto>>();

        var result = await handler.HandleAsync(
            new CreateOrganizationCommand("Harbor House", "harbor-house", "subject-a", "user:subject-a"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(repository.Organizations);
        OrganizationMembership membership = Assert.Single(repository.Memberships);
        Assert.Equal(result.Value.Organization.OrganizationId, membership.OrganizationId);
        Assert.Equal("subject-a", membership.SubjectId);
        Assert.Equal(OrganizationMembershipRole.Owner, result.Value.Membership.Role);
    }

    private static ServiceProvider CreateServices(TestRepository repository, bool enabled)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Organizations:SelfServiceCreationEnabled"] = enabled.ToString()
            })
            .Build();
        ServiceCollection services = new();
        services.AddOrganizationsApplication(configuration);
        services.AddSingleton<IOrganizationRepository>(repository);
        services.AddSingleton<ISystemClock>(new TestClock());
        services.AddSingleton<IIdGenerator>(new TestIds());
        return services.BuildServiceProvider();
    }

    private sealed class TestRepository : IOrganizationRepository
    {
        public List<Organization> Organizations { get; } = [];
        public List<OrganizationMembership> Memberships { get; } = [];

        public Task<Organization?> GetOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult(this.Organizations.SingleOrDefault(item => item.Id == organizationId));

        public Task<OrganizationMembership?> GetMembershipAsync(Guid organizationId, string subjectId, CancellationToken cancellationToken) =>
            Task.FromResult(this.Memberships.SingleOrDefault(item => item.OrganizationId == organizationId && item.SubjectId == subjectId));

        public Task<OrganizationInvitation?> GetInvitationAsync(Guid organizationId, Guid invitationId, CancellationToken cancellationToken) => Task.FromResult<OrganizationInvitation?>(null);
        public Task<bool> InvitationIdExistsAsync(Guid invitationId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<OrganizationInvitation?> GetInvitationByDigestAsync(string tokenDigest, CancellationToken cancellationToken) => Task.FromResult<OrganizationInvitation?>(null);
        public Task<OrganizationEnrollmentLink?> GetEnrollmentLinkAsync(Guid organizationId, Guid enrollmentLinkId, CancellationToken cancellationToken) => Task.FromResult<OrganizationEnrollmentLink?>(null);
        public Task<bool> EnrollmentLinkIdExistsAsync(Guid enrollmentLinkId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<OrganizationEnrollmentLink?> GetEnrollmentLinkByDigestAsync(string tokenDigest, CancellationToken cancellationToken) => Task.FromResult<OrganizationEnrollmentLink?>(null);
        public Task<OrganizationEnrollmentClaim?> GetEnrollmentClaimAsync(Guid organizationId, Guid claimId, CancellationToken cancellationToken) => Task.FromResult<OrganizationEnrollmentClaim?>(null);
        public Task<OrganizationEnrollmentClaim?> GetEnrollmentClaimBySubjectAsync(Guid enrollmentLinkId, string subjectId, CancellationToken cancellationToken) => Task.FromResult<OrganizationEnrollmentClaim?>(null);

        public Task<bool> SlugExistsAsync(string slug, Guid? excludingOrganizationId, CancellationToken cancellationToken) =>
            Task.FromResult(this.Organizations.Any(item => item.Slug == slug && item.Id != excludingOrganizationId));

        public Task<bool> MembershipExistsAsync(Guid organizationId, string subjectId, CancellationToken cancellationToken) =>
            Task.FromResult(this.Memberships.Any(item => item.OrganizationId == organizationId && item.SubjectId == subjectId));

        public Task<OrganizationListResponse> ListForSubjectAsync(string subjectId, int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new OrganizationListResponse([], page, pageSize));
        public Task<OrganizationCatalogListResponse> ListCatalogAsync(int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new OrganizationCatalogListResponse([], page, pageSize));

        public Task<OrganizationMemberListResponse> ListMembersAsync(Guid organizationId, int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new OrganizationMemberListResponse([], page, pageSize));

        public Task<OrganizationInvitationListResponse> ListInvitationsAsync(Guid organizationId, int page, int pageSize, DateTimeOffset nowUtc, CancellationToken cancellationToken) =>
            Task.FromResult(new OrganizationInvitationListResponse([], page, pageSize));
        public Task<OrganizationEnrollmentLinkListResponse> ListEnrollmentLinksAsync(Guid organizationId, int page, int pageSize, DateTimeOffset nowUtc, CancellationToken cancellationToken) =>
            Task.FromResult(new OrganizationEnrollmentLinkListResponse([], page, pageSize));
        public Task<OrganizationJoinRequestListResponse> ListPendingJoinRequestsAsync(
            Guid organizationId, int page, int pageSize, DateTimeOffset nowUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult(new OrganizationJoinRequestListResponse([], page, pageSize));

        public Task AddOrganizationAsync(Organization organization, CancellationToken cancellationToken)
        {
            this.Organizations.Add(organization);
            return Task.CompletedTask;
        }

        public Task AddMembershipAsync(OrganizationMembership membership, CancellationToken cancellationToken)
        {
            this.Memberships.Add(membership);
            return Task.CompletedTask;
        }

        public Task AddInvitationAsync(OrganizationInvitation invitation, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddEnrollmentLinkAsync(OrganizationEnrollmentLink value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddEnrollmentClaimAsync(OrganizationEnrollmentClaim value, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class TestIds : IIdGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }
}
