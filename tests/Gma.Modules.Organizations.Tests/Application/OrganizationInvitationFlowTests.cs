namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
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
public sealed class OrganizationInvitationFlowTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Unbound_invitation_acceptance_is_idempotent_and_secret_is_not_persisted()
    {
        TestRepository repository = CreateRepository();
        TestClock clock = new();
        using ServiceProvider services = CreateServices(repository, clock);
        var create = services.GetRequiredService<
            ICommandHandler<CreateOrganizationInvitationCommand, OrganizationInvitationIssuedDto>>();
        var accept = services.GetRequiredService<
            ICommandHandler<AcceptOrganizationInvitationCommand, OrganizationInvitationAcceptanceDto>>();
        Organization organization = Assert.Single(repository.Organizations);

        var issued = await create.HandleAsync(new CreateOrganizationInvitationCommand(
            organization.Id, null, null, "owner", "user:owner"), CancellationToken.None);
        Assert.True(issued.IsSuccess);
        OrganizationInvitation stored = Assert.Single(repository.Invitations);
        Assert.NotEqual(issued.Value.Token, stored.TokenDigest);
        Assert.Equal(43, issued.Value.Token.Length);
        Assert.Equal(64, stored.TokenDigest.Length);

        var first = await accept.HandleAsync(new AcceptOrganizationInvitationCommand(
            issued.Value.Token, "member", "user:member"), CancellationToken.None);
        var retry = await accept.HandleAsync(new AcceptOrganizationInvitationCommand(
            issued.Value.Token, "member", "user:member"), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Single(repository.Memberships, membership => membership.SubjectId == "member");
        Assert.Equal(first.Value.Membership.Membership.MembershipId,
            retry.Value.Membership.Membership.MembershipId);
    }

    [Fact]
    public async Task Bound_invitation_fails_closed_without_recipient_verification_extension()
    {
        TestRepository repository = CreateRepository();
        TestClock clock = new();
        using ServiceProvider services = CreateServices(repository, clock);
        var create = services.GetRequiredService<
            ICommandHandler<CreateOrganizationInvitationCommand, OrganizationInvitationIssuedDto>>();
        var accept = services.GetRequiredService<
            ICommandHandler<AcceptOrganizationInvitationCommand, OrganizationInvitationAcceptanceDto>>();
        Organization organization = Assert.Single(repository.Organizations);
        var issued = await create.HandleAsync(new CreateOrganizationInvitationCommand(
            organization.Id, "member@example.com", 24, "owner", "user:owner"), CancellationToken.None);

        var result = await accept.HandleAsync(new AcceptOrganizationInvitationCommand(
            issued.Value.Token, "member", "user:member"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(OrganizationApplicationErrors.RecipientVerificationRequired, result.Error);
        Assert.Null(Assert.Single(repository.Invitations).AcceptedSubjectId);
    }

    [Fact]
    public async Task Accepted_token_rejects_a_competing_subject()
    {
        TestRepository repository = CreateRepository();
        TestClock clock = new();
        using ServiceProvider services = CreateServices(repository, clock);
        var create = services.GetRequiredService<
            ICommandHandler<CreateOrganizationInvitationCommand, OrganizationInvitationIssuedDto>>();
        var accept = services.GetRequiredService<
            ICommandHandler<AcceptOrganizationInvitationCommand, OrganizationInvitationAcceptanceDto>>();
        Organization organization = Assert.Single(repository.Organizations);
        var issued = await create.HandleAsync(new CreateOrganizationInvitationCommand(
            organization.Id, null, 24, "owner", "user:owner"), CancellationToken.None);
        Assert.True((await accept.HandleAsync(new AcceptOrganizationInvitationCommand(
            issued.Value.Token, "first", "user:first"), CancellationToken.None)).IsSuccess);

        var competing = await accept.HandleAsync(new AcceptOrganizationInvitationCommand(
            issued.Value.Token, "second", "user:second"), CancellationToken.None);

        Assert.True(competing.IsFailure);
        Assert.Equal(OrganizationDomainErrors.InvitationClaimedByAnotherSubject, competing.Error);
        Assert.DoesNotContain(repository.Memberships, membership => membership.SubjectId == "second");
    }

    [Fact]
    public async Task Expired_invitation_cannot_create_a_membership()
    {
        TestRepository repository = CreateRepository();
        TestClock clock = new();
        using ServiceProvider services = CreateServices(repository, clock);
        var create = services.GetRequiredService<
            ICommandHandler<CreateOrganizationInvitationCommand, OrganizationInvitationIssuedDto>>();
        var accept = services.GetRequiredService<
            ICommandHandler<AcceptOrganizationInvitationCommand, OrganizationInvitationAcceptanceDto>>();
        Organization organization = Assert.Single(repository.Organizations);
        var issued = await create.HandleAsync(new CreateOrganizationInvitationCommand(
            organization.Id, null, 1, "owner", "user:owner"), CancellationToken.None);
        clock.UtcNow = Now.AddHours(2);

        var result = await accept.HandleAsync(new AcceptOrganizationInvitationCommand(
            issued.Value.Token, "member", "user:member"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(OrganizationDomainErrors.InvitationExpired, result.Error);
        Assert.DoesNotContain(repository.Memberships, membership => membership.SubjectId == "member");
    }

    [Fact]
    public async Task Product_policy_denies_a_fresh_acceptance_but_not_an_accepted_retry()
    {
        TestRepository repository = CreateRepository();
        TestClock clock = new();
        RecordingJoinPolicy policy = new();
        using ServiceProvider services = CreateServices(repository, clock, policy);
        var create = services.GetRequiredService<
            ICommandHandler<CreateOrganizationInvitationCommand, OrganizationInvitationIssuedDto>>();
        var accept = services.GetRequiredService<
            ICommandHandler<AcceptOrganizationInvitationCommand, OrganizationInvitationAcceptanceDto>>();
        Organization organization = Assert.Single(repository.Organizations);
        var issued = await create.HandleAsync(new CreateOrganizationInvitationCommand(
            organization.Id, null, 24, "owner", "user:owner"), CancellationToken.None);
        policy.IsAllowed = false;

        var denied = await accept.HandleAsync(new AcceptOrganizationInvitationCommand(
            issued.Value.Token, "member", "user:member"), CancellationToken.None);
        Assert.True(denied.IsFailure);
        Assert.Equal(OrganizationApplicationErrors.JoinAdmissionRejected, denied.Error);
        Assert.DoesNotContain(repository.Memberships, membership => membership.SubjectId == "member");
        Assert.Null(Assert.Single(repository.Invitations).AcceptedSubjectId);

        policy.IsAllowed = true;
        Assert.True((await accept.HandleAsync(new AcceptOrganizationInvitationCommand(
            issued.Value.Token, "member", "user:member"), CancellationToken.None)).IsSuccess);
        policy.IsAllowed = false;
        var retry = await accept.HandleAsync(new AcceptOrganizationInvitationCommand(
            issued.Value.Token, "member", "user:member"), CancellationToken.None);

        Assert.True(retry.IsSuccess);
        Assert.Equal(2, policy.Contexts.Count);
        OrganizationJoinAdmissionContext context = policy.Contexts[^1];
        Assert.Equal(OrganizationJoinAdmissionOperation.AcceptInvitation, context.Operation);
        Assert.Equal(issued.Value.Invitation.InvitationId, context.SourceId);
        Assert.Equal("member", context.ApplicantSubjectId);
    }

    [Fact]
    public async Task Suspended_organization_blocks_reissue_but_allows_revocation()
    {
        TestRepository repository = CreateRepository();
        TestClock clock = new();
        using ServiceProvider services = CreateServices(repository, clock);
        var create = services.GetRequiredService<
            ICommandHandler<CreateOrganizationInvitationCommand, OrganizationInvitationIssuedDto>>();
        var reissue = services.GetRequiredService<
            ICommandHandler<ReissueOrganizationInvitationCommand, OrganizationInvitationIssuedDto>>();
        var revoke = services.GetRequiredService<
            ICommandHandler<RevokeOrganizationInvitationCommand, OrganizationInvitationDto>>();
        Organization organization = Assert.Single(repository.Organizations);
        OrganizationInvitationIssuedDto issued = (await create.HandleAsync(
            new CreateOrganizationInvitationCommand(
                organization.Id, null, 24, "owner", "user:owner"),
            CancellationToken.None)).Value;
        clock.UtcNow = Now.AddMinutes(1);
        Assert.True(organization.Suspend(
            organization.Version, "user:owner", Guid.NewGuid(), clock.UtcNow).IsSuccess);

        Result<OrganizationInvitationIssuedDto> replacement = await reissue.HandleAsync(
            new ReissueOrganizationInvitationCommand(
                organization.Id, issued.Invitation.InvitationId, issued.Invitation.Version,
                24, "owner", "user:owner"),
            CancellationToken.None);
        Result<OrganizationInvitationDto> revoked = await revoke.HandleAsync(
            new RevokeOrganizationInvitationCommand(
                organization.Id, issued.Invitation.InvitationId, issued.Invitation.Version,
                "owner", "user:owner"),
            CancellationToken.None);

        Assert.True(replacement.IsFailure);
        Assert.Equal(OrganizationDomainErrors.OrganizationNotActive, replacement.Error);
        Assert.True(revoked.IsSuccess);
        Assert.Single(repository.Invitations);
        Assert.Equal(OrganizationInvitationStatus.Revoked, revoked.Value.Status);
    }

    private static TestRepository CreateRepository()
    {
        Organization organization = Organization.Create(
            Guid.NewGuid(), "Harbor House", "harbor-house", "user:owner", Guid.NewGuid(), Now).Value;
        OrganizationMembership owner = OrganizationMembership.Create(
            Guid.NewGuid(), organization.Id, "owner", DomainMembershipRole.Owner,
            "user:owner", Guid.NewGuid(), Now).Value;
        return new TestRepository(organization, owner);
    }

    private static ServiceProvider CreateServices(
        TestRepository repository,
        TestClock clock,
        IOrganizationJoinAdmissionPolicy? joinPolicy = null)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Organizations:SelfServiceCreationEnabled"] = "true",
                ["Organizations:InvitationDefaultLifetimeHours"] = "168",
                ["Organizations:InvitationMaxLifetimeHours"] = "720"
            })
            .Build();
        ServiceCollection services = new();
        services.AddOrganizationsApplication(configuration);
        if (joinPolicy is not null)
        {
            services.AddSingleton(joinPolicy);
        }
        services.AddSingleton<IOrganizationRepository>(repository);
        services.AddSingleton<ISystemClock>(clock);
        services.AddSingleton<IIdGenerator>(new TestIds());
        return services.BuildServiceProvider();
    }

    private sealed class RecordingJoinPolicy : IOrganizationJoinAdmissionPolicy
    {
        public bool IsAllowed { get; set; } = true;
        public List<OrganizationJoinAdmissionContext> Contexts { get; } = [];

        public ValueTask<bool> IsAllowedAsync(
            OrganizationJoinAdmissionContext context,
            CancellationToken cancellationToken = default)
        {
            this.Contexts.Add(context);
            return ValueTask.FromResult(this.IsAllowed);
        }
    }

    private sealed class TestRepository(Organization organization, OrganizationMembership owner)
        : IOrganizationRepository
    {
        public List<Organization> Organizations { get; } = [organization];
        public List<OrganizationMembership> Memberships { get; } = [owner];
        public List<OrganizationInvitation> Invitations { get; } = [];

        public Task<Organization?> GetOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult(this.Organizations.SingleOrDefault(item => item.Id == organizationId));
        public Task<OrganizationMembership?> GetMembershipAsync(Guid organizationId, string subjectId, CancellationToken cancellationToken) =>
            Task.FromResult(this.Memberships.SingleOrDefault(item => item.OrganizationId == organizationId && item.SubjectId == subjectId));
        public Task<OrganizationInvitation?> GetInvitationAsync(Guid organizationId, Guid invitationId, CancellationToken cancellationToken) =>
            Task.FromResult(this.Invitations.SingleOrDefault(item => item.OrganizationId == organizationId && item.Id == invitationId));
        public Task<OrganizationInvitation?> GetInvitationByDigestAsync(string tokenDigest, CancellationToken cancellationToken) =>
            Task.FromResult(this.Invitations.SingleOrDefault(item => item.TokenDigest == tokenDigest));
        public Task<OrganizationEnrollmentLink?> GetEnrollmentLinkAsync(Guid organizationId, Guid enrollmentLinkId, CancellationToken cancellationToken) => Task.FromResult<OrganizationEnrollmentLink?>(null);
        public Task<OrganizationEnrollmentLink?> GetEnrollmentLinkByDigestAsync(string tokenDigest, CancellationToken cancellationToken) => Task.FromResult<OrganizationEnrollmentLink?>(null);
        public Task<OrganizationEnrollmentClaim?> GetEnrollmentClaimAsync(Guid organizationId, Guid claimId, CancellationToken cancellationToken) => Task.FromResult<OrganizationEnrollmentClaim?>(null);
        public Task<OrganizationEnrollmentClaim?> GetEnrollmentClaimBySubjectAsync(Guid enrollmentLinkId, string subjectId, CancellationToken cancellationToken) => Task.FromResult<OrganizationEnrollmentClaim?>(null);
        public Task<bool> SlugExistsAsync(string slug, Guid? excludingOrganizationId, CancellationToken cancellationToken) => Task.FromResult(false);
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
        public Task<OrganizationJoinRequestListResponse> ListPendingJoinRequestsAsync(Guid organizationId, int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new OrganizationJoinRequestListResponse([], page, pageSize));
        public Task AddOrganizationAsync(Organization value, CancellationToken cancellationToken)
        {
            this.Organizations.Add(value);
            return Task.CompletedTask;
        }
        public Task AddMembershipAsync(OrganizationMembership value, CancellationToken cancellationToken)
        {
            this.Memberships.Add(value);
            return Task.CompletedTask;
        }
        public Task AddInvitationAsync(OrganizationInvitation value, CancellationToken cancellationToken)
        {
            this.Invitations.Add(value);
            return Task.CompletedTask;
        }
        public Task AddEnrollmentLinkAsync(OrganizationEnrollmentLink value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddEnrollmentClaimAsync(OrganizationEnrollmentClaim value, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = Now;
    }

    private sealed class TestIds : IIdGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }
}
