namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Framework.Cqrs;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using DomainMembershipRole = Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;
using Gma.Modules.Organizations.Domain.Errors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class MembershipGovernanceHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Suspending_the_last_owner_fails_without_mutating_membership()
    {
        TestRepository repository = CreateRepository(includeMember: false);
        using ServiceProvider services = CreateServices(repository);
        var handler = services.GetRequiredService<
            ICommandHandler<ChangeOrganizationMembershipCommand, OrganizationMembershipDto>>();
        Organization organization = Assert.Single(repository.Organizations);
        OrganizationMembership owner = Assert.Single(repository.Memberships);

        var result = await handler.HandleAsync(new ChangeOrganizationMembershipCommand(
            organization.Id, owner.SubjectId, OrganizationMembershipAction.Suspend,
            organization.Version, owner.Version, owner.SubjectId, "user:owner"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(OrganizationDomainErrors.LastActiveOwner, result.Error);
        Assert.Equal(Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipState.Active, owner.Status);
        Assert.Equal(1, organization.ActiveOwnerCount);
    }

    [Fact]
    public async Task Transfer_promotes_target_and_demotes_current_owner_with_stable_owner_count()
    {
        TestRepository repository = CreateRepository(includeMember: true);
        using ServiceProvider services = CreateServices(repository);
        var handler = services.GetRequiredService<
            ICommandHandler<TransferOrganizationOwnershipCommand, OrganizationMembershipDto>>();
        Organization organization = Assert.Single(repository.Organizations);
        OrganizationMembership owner = repository.Memberships.Single(item => item.Role == DomainMembershipRole.Owner);
        OrganizationMembership member = repository.Memberships.Single(item => item.Role == DomainMembershipRole.Member);

        var result = await handler.HandleAsync(new TransferOrganizationOwnershipCommand(
            organization.Id, member.SubjectId, organization.Version, owner.Version, member.Version,
            owner.SubjectId, "user:owner"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DomainMembershipRole.Member, owner.Role);
        Assert.Equal(DomainMembershipRole.Owner, member.Role);
        Assert.Equal(1, organization.ActiveOwnerCount);
        Assert.Equal(2, organization.Version);
    }

    [Fact]
    public async Task Transfer_to_self_is_rejected_before_state_changes()
    {
        TestRepository repository = CreateRepository(includeMember: false);
        using ServiceProvider services = CreateServices(repository);
        var handler = services.GetRequiredService<
            ICommandHandler<TransferOrganizationOwnershipCommand, OrganizationMembershipDto>>();
        Organization organization = Assert.Single(repository.Organizations);
        OrganizationMembership owner = Assert.Single(repository.Memberships);

        var result = await handler.HandleAsync(new TransferOrganizationOwnershipCommand(
            organization.Id, owner.SubjectId, organization.Version, owner.Version, owner.Version,
            owner.SubjectId, "user:owner"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(OrganizationApplicationErrors.OwnershipTargetMustDiffer, result.Error);
        Assert.Equal(DomainMembershipRole.Owner, owner.Role);
    }

    private static TestRepository CreateRepository(bool includeMember)
    {
        Organization organization = Organization.Create(
            Guid.NewGuid(), "Harbor House", "harbor-house", "user:owner", Guid.NewGuid(), Now).Value;
        OrganizationMembership owner = OrganizationMembership.Create(
            Guid.NewGuid(), organization.Id, "owner", DomainMembershipRole.Owner,
            "user:owner", Guid.NewGuid(), Now).Value;
        TestRepository repository = new(organization, owner);
        if (includeMember)
        {
            repository.Memberships.Add(OrganizationMembership.Create(
                Guid.NewGuid(), organization.Id, "member", DomainMembershipRole.Member,
                "user:owner", Guid.NewGuid(), Now).Value);
        }

        return repository;
    }

    private static ServiceProvider CreateServices(TestRepository repository)
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();
        services.AddOrganizationsApplication(configuration);
        services.AddSingleton<IOrganizationRepository>(repository);
        services.AddSingleton<ISystemClock>(new TestClock());
        services.AddSingleton<IIdGenerator>(new TestIds());
        return services.BuildServiceProvider();
    }

    private sealed class TestRepository(Organization organization, OrganizationMembership owner)
        : IOrganizationRepository
    {
        public List<Organization> Organizations { get; } = [organization];
        public List<OrganizationMembership> Memberships { get; } = [owner];

        public Task<Organization?> GetOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult(this.Organizations.SingleOrDefault(item => item.Id == organizationId));
        public Task<OrganizationMembership?> GetMembershipAsync(Guid organizationId, string subjectId, CancellationToken cancellationToken) =>
            Task.FromResult(this.Memberships.SingleOrDefault(item => item.OrganizationId == organizationId && item.SubjectId == subjectId));
        public Task<OrganizationInvitation?> GetInvitationAsync(Guid organizationId, Guid invitationId, CancellationToken cancellationToken) => Task.FromResult<OrganizationInvitation?>(null);
        public Task<OrganizationInvitation?> GetInvitationByDigestAsync(string tokenDigest, CancellationToken cancellationToken) => Task.FromResult<OrganizationInvitation?>(null);
        public Task<OrganizationEnrollmentLink?> GetEnrollmentLinkAsync(Guid organizationId, Guid enrollmentLinkId, CancellationToken cancellationToken) => Task.FromResult<OrganizationEnrollmentLink?>(null);
        public Task<OrganizationEnrollmentLink?> GetEnrollmentLinkByDigestAsync(string tokenDigest, CancellationToken cancellationToken) => Task.FromResult<OrganizationEnrollmentLink?>(null);
        public Task<OrganizationEnrollmentClaim?> GetEnrollmentClaimAsync(Guid organizationId, Guid claimId, CancellationToken cancellationToken) => Task.FromResult<OrganizationEnrollmentClaim?>(null);
        public Task<OrganizationEnrollmentClaim?> GetEnrollmentClaimBySubjectAsync(Guid enrollmentLinkId, string subjectId, CancellationToken cancellationToken) => Task.FromResult<OrganizationEnrollmentClaim?>(null);
        public Task<bool> SlugExistsAsync(string slug, Guid? excludingOrganizationId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> MembershipExistsAsync(Guid organizationId, string subjectId, CancellationToken cancellationToken) => Task.FromResult(false);
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
        public Task<OrganizationJoinRequestListResponse> ListPendingJoinRequestsAsync(Guid organizationId, int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new OrganizationJoinRequestListResponse([], page, pageSize));
        public Task AddOrganizationAsync(Organization value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddMembershipAsync(OrganizationMembership value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddInvitationAsync(OrganizationInvitation value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddEnrollmentLinkAsync(OrganizationEnrollmentLink value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddEnrollmentClaimAsync(OrganizationEnrollmentClaim value, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now.AddMinutes(1);
    }

    private sealed class TestIds : IIdGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }
}
