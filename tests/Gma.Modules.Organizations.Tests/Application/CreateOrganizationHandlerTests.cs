namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
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
    private static readonly DateTimeOffset Now =
        new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero)
            .AddTicks(7);

    [Fact]
    public async Task Self_service_creation_is_disabled_by_default()
    {
        TestRepository repository = new();
        using ServiceProvider services = CreateServices(repository, enabled: false);
        var handler = services.GetRequiredService<
            ICommandHandler<CreateOrganizationCommand, OrganizationMembershipSummaryDto>>();

        var result = await handler.HandleAsync(
            CreateCommand(Guid.NewGuid()),
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
        Guid operationId = Guid.NewGuid();

        var result = await handler.HandleAsync(
            CreateCommand(operationId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Organization organization = Assert.Single(repository.Organizations);
        Assert.Equal(operationId, organization.Id);
        Assert.Equal(64, organization.CreationRequestFingerprint?.Length);
        Assert.Equal(0, organization.CreatedAtUtc.Ticks % 10);
        OrganizationMembership membership = Assert.Single(repository.Memberships);
        Assert.Equal(result.Value.Organization.OrganizationId, membership.OrganizationId);
        Assert.Equal("subject-a", membership.SubjectId);
        Assert.Equal(OrganizationMembershipRole.Owner, result.Value.Membership.Role);
    }

    [Fact]
    public async Task Exact_retry_returns_current_state_without_reapplying_admission()
    {
        Guid operationId = Guid.NewGuid();
        TestRepository repository = new();
        using (ServiceProvider enabled = CreateServices(repository, enabled: true))
        {
            var handler = enabled.GetRequiredService<
                ICommandHandler<CreateOrganizationCommand, OrganizationMembershipSummaryDto>>();
            var created = await handler.HandleAsync(
                CreateCommand(operationId),
                CancellationToken.None);
            Assert.True(created.IsSuccess, created.Error.Code);
            Assert.True(repository.Organizations[0].UpdateProfile(
                "Harbor House Updated",
                "harbor-house-updated",
                expectedVersion: 1,
                "user:subject-a",
                Guid.NewGuid(),
                Now.AddMinutes(1)).IsSuccess);
        }

        using ServiceProvider disabled = CreateServices(
            repository,
            enabled: false);
        var replayHandler = disabled.GetRequiredService<
            ICommandHandler<
                CreateOrganizationCommand,
                OrganizationMembershipSummaryDto>>();

        var replayed = await replayHandler.HandleAsync(
            CreateCommand(operationId) with
            {
                Name = " Harbor House ",
                Slug = " HARBOR-HOUSE ",
                SubjectId = "subject-a ",
                ActorId = " user:subject-a "
            },
            CancellationToken.None);

        Assert.True(replayed.IsSuccess, replayed.Error.Code);
        Assert.Equal("Harbor House Updated", replayed.Value.Organization.Name);
        Assert.Single(repository.Organizations);
        Assert.Single(repository.Memberships);
    }

    [Fact]
    public async Task Changed_or_legacy_operation_reuse_conflicts()
    {
        Guid operationId = Guid.NewGuid();
        TestRepository repository = new();
        using ServiceProvider services = CreateServices(
            repository,
            enabled: true);
        var handler = services.GetRequiredService<
            ICommandHandler<
                CreateOrganizationCommand,
                OrganizationMembershipSummaryDto>>();
        var created = await handler.HandleAsync(
            CreateCommand(operationId),
            CancellationToken.None);
        Assert.True(created.IsSuccess, created.Error.Code);

        var changed = await handler.HandleAsync(
            CreateCommand(operationId) with { Name = "Another House" },
            CancellationToken.None);
        Guid legacyId = Guid.NewGuid();
        repository.Organizations.Add(Organization.Create(
            legacyId,
            "Legacy House",
            "legacy-house",
            "user:subject-a",
            Guid.NewGuid(),
            Now).Value);
        repository.Memberships.Add(OrganizationMembership.Create(
            Guid.NewGuid(),
            legacyId,
            "subject-a",
            Gma.Modules.Organizations.Domain.Enums
                .OrganizationMembershipRole.Owner,
            "user:subject-a",
            Guid.NewGuid(),
            Now).Value);
        var legacy = await handler.HandleAsync(
            CreateCommand(legacyId) with
            {
                Name = "Legacy House",
                Slug = "legacy-house"
            },
            CancellationToken.None);

        Assert.Equal(
            OrganizationApplicationErrors.CreationOperationConflict,
            changed.Error);
        Assert.Equal(
            OrganizationApplicationErrors.CreationOperationConflict,
            legacy.Error);
        Assert.Equal(2, repository.Organizations.Count);
        Assert.Equal(2, repository.Memberships.Count);
    }

    [Fact]
    public async Task Retry_requires_an_active_result_membership()
    {
        Guid operationId = Guid.NewGuid();
        TestRepository repository = new();
        using ServiceProvider services = CreateServices(
            repository,
            enabled: true);
        var handler = services.GetRequiredService<
            ICommandHandler<
                CreateOrganizationCommand,
                OrganizationMembershipSummaryDto>>();
        var created = await handler.HandleAsync(
            CreateCommand(operationId),
            CancellationToken.None);
        Assert.True(created.IsSuccess, created.Error.Code);
        OrganizationMembership membership = Assert.Single(repository.Memberships);
        Assert.True(membership.Suspend(
            membership.Version,
            "user:subject-a",
            Guid.NewGuid(),
            Now.AddMinutes(1)).IsSuccess);

        var replayed = await handler.HandleAsync(
            CreateCommand(operationId),
            CancellationToken.None);

        Assert.Equal(
            OrganizationApplicationErrors.CreationOperationConflict,
            replayed.Error);
    }

    [Fact]
    public async Task Failed_validation_or_admission_does_not_bind_an_operation()
    {
        Guid operationId = Guid.NewGuid();
        TestRepository repository = new();
        using (ServiceProvider disabled = CreateServices(
                   repository,
                   enabled: false))
        {
            var handler = disabled.GetRequiredService<
                ICommandHandler<
                    CreateOrganizationCommand,
                    OrganizationMembershipSummaryDto>>();
            var missingOperation = await handler.HandleAsync(
                CreateCommand(Guid.Empty),
                CancellationToken.None);
            var denied = await handler.HandleAsync(
                CreateCommand(operationId),
                CancellationToken.None);

            Assert.Equal(
                OrganizationApplicationErrors.CreationOperationRequired,
                missingOperation.Error);
            Assert.Equal(
                OrganizationApplicationErrors.SelfServiceCreationDisabled,
                denied.Error);
            Assert.Empty(repository.Organizations);
        }

        using ServiceProvider enabled = CreateServices(
            repository,
            enabled: true);
        var retryHandler = enabled.GetRequiredService<
            ICommandHandler<
                CreateOrganizationCommand,
                OrganizationMembershipSummaryDto>>();
        var retried = await retryHandler.HandleAsync(
            CreateCommand(operationId),
            CancellationToken.None);

        Assert.True(retried.IsSuccess, retried.Error.Code);
        Assert.Single(repository.Organizations);
    }

    private static CreateOrganizationCommand CreateCommand(Guid operationId) =>
        new(
            operationId,
            "Harbor House",
            "harbor-house",
            "subject-a",
            "user:subject-a");

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
        services.AddSingleton<IOrganizationCreationCoordinator>(
            new TestCreationCoordinator(repository));
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

        public Task<OrganizationListResponse> ListForSubjectAsync(string subjectId, PageRequest pageRequest, CancellationToken cancellationToken) =>
            Task.FromResult(new OrganizationListResponse([], pageRequest.Page, pageRequest.PageSize));
        public Task<OrganizationCatalogListResponse> ListCatalogAsync(PageRequest pageRequest, CancellationToken cancellationToken) =>
            Task.FromResult(new OrganizationCatalogListResponse([], pageRequest.Page, pageRequest.PageSize));

        public Task<OrganizationMemberListResponse> ListMembersAsync(Guid organizationId, PageRequest pageRequest, CancellationToken cancellationToken) =>
            Task.FromResult(new OrganizationMemberListResponse([], pageRequest.Page, pageRequest.PageSize));

        public Task<OrganizationInvitationListResponse> ListInvitationsAsync(Guid organizationId, PageRequest pageRequest, DateTimeOffset nowUtc, CancellationToken cancellationToken) =>
            Task.FromResult(new OrganizationInvitationListResponse([], pageRequest.Page, pageRequest.PageSize));
        public Task<OrganizationEnrollmentLinkListResponse> ListEnrollmentLinksAsync(Guid organizationId, PageRequest pageRequest, DateTimeOffset nowUtc, CancellationToken cancellationToken) =>
            Task.FromResult(new OrganizationEnrollmentLinkListResponse([], pageRequest.Page, pageRequest.PageSize));
        public Task<OrganizationJoinRequestListResponse> ListPendingJoinRequestsAsync(
            Guid organizationId, PageRequest pageRequest, DateTimeOffset nowUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult(new OrganizationJoinRequestListResponse(
                [],
                pageRequest.Page,
                pageRequest.PageSize));

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
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestCreationCoordinator(TestRepository repository)
        : IOrganizationCreationCoordinator
    {
        public Task<Organization?> AcquireAsync(
            Guid operationId,
            CancellationToken cancellationToken) =>
            repository.GetOrganizationAsync(operationId, cancellationToken);
    }

    private sealed class TestIds : IIdGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }
}
