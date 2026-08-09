namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Api;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using DomainMembershipRole = Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;
using DomainInvitationState = Gma.Modules.Organizations.Domain.Enums.OrganizationInvitationState;
using Gma.Modules.Organizations.Domain.Errors;
using Gma.Modules.Organizations.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationInvitationFlowTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Contracts_issuance_is_idempotent_and_returns_the_secret_only_once()
    {
        TestRepository repository = CreateRepository();
        TestClock clock = new();
        using ServiceProvider services = CreateServices(repository, clock);
        var issue = services.GetRequiredService<ICommandHandler<
            IssueOrganizationInvitationCommand,
            OrganizationJoinSourceIssuance<OrganizationInvitationDto>>>();
        Organization organization = Assert.Single(repository.Organizations);
        Guid sourceId = Guid.NewGuid();
        OrganizationInvitationIssuanceRequest request = new(
            sourceId,
            organization.Id,
            " Member@Example.com ",
            24,
            "owner",
            "user:owner");

        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> first =
            await issue.HandleAsync(new IssueOrganizationInvitationCommand(request), CancellationToken.None);
        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> replay =
            await issue.HandleAsync(new IssueOrganizationInvitationCommand(request), CancellationToken.None);
        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> conflict =
            await issue.HandleAsync(
                new IssueOrganizationInvitationCommand(request with { RecipientEmail = "other@example.com" }),
                CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(first.Value.IsSuccess);
        Assert.Equal(OrganizationJoinSourceIssuanceOutcome.Issued, first.Value.Outcome);
        Assert.True(first.Value.HasNewToken);
        Assert.Equal(43, first.Value.Token!.Length);
        Assert.True(replay.IsSuccess);
        Assert.True(replay.Value.IsSuccess);
        Assert.Equal(OrganizationJoinSourceIssuanceOutcome.AlreadyIssued, replay.Value.Outcome);
        Assert.Null(replay.Value.Token);
        Assert.False(replay.Value.HasNewToken);
        Assert.True(conflict.IsFailure);
        Assert.Equal(OrganizationApplicationErrors.JoinSourceIssuanceConflict, conflict.Error);
        OrganizationInvitation stored = Assert.Single(repository.Invitations);
        Assert.Equal(sourceId, stored.Id);
        Assert.Equal("member@example.com", stored.RecipientEmail);
        Assert.NotEqual(first.Value.Token, stored.TokenDigest);
    }

    [Fact]
    public async Task Contracts_issuance_rejects_a_source_id_owned_by_another_organization()
    {
        TestRepository repository = CreateRepository();
        TestClock clock = new();
        using ServiceProvider services = CreateServices(repository, clock);
        var issue = services.GetRequiredService<ICommandHandler<
            IssueOrganizationInvitationCommand,
            OrganizationJoinSourceIssuance<OrganizationInvitationDto>>>();
        Organization organization = Assert.Single(repository.Organizations);
        Guid sourceId = Guid.NewGuid();
        repository.Invitations.Add(OrganizationInvitation.Create(
            sourceId,
            Guid.NewGuid(),
            "other-owner",
            null,
            new string('a', 64),
            Now.AddHours(24),
            "user:other-owner",
            Guid.NewGuid(),
            Now).Value);

        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> result =
            await issue.HandleAsync(
                new IssueOrganizationInvitationCommand(new OrganizationInvitationIssuanceRequest(
                    sourceId,
                    organization.Id,
                    null,
                    24,
                    "owner",
                    "user:owner")),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(OrganizationApplicationErrors.JoinSourceIssuanceConflict, result.Error);
        Assert.Single(repository.Invitations);
    }

    [Fact]
    public async Task Reissue_is_idempotent_records_lineage_and_never_replays_the_secret()
    {
        TestRepository repository = CreateRepository();
        TestClock clock = new();
        using ServiceProvider services = CreateServices(repository, clock);
        Organization organization = Assert.Single(repository.Organizations);
        OrganizationInvitationIssuedDto issued = await IssueAsync(
            services, organization, "member@example.com", 24);
        var reissue = services.GetRequiredService<ICommandHandler<
            ReissueOrganizationInvitationCommand,
            OrganizationJoinSourceIssuance<OrganizationInvitationDto>>>();
        Guid replacementId = Guid.NewGuid();
        ReissueOrganizationInvitationCommand command = new(
            organization.Id,
            issued.Invitation.InvitationId,
            replacementId,
            issued.Invitation.Version,
            48,
            "owner",
            "user:owner");

        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> first =
            await reissue.HandleAsync(command, CancellationToken.None);
        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> replay =
            await reissue.HandleAsync(command, CancellationToken.None);
        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> changed =
            await reissue.HandleAsync(command with { LifetimeHours = 72 }, CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error.Code);
        Assert.Equal(OrganizationJoinSourceIssuanceOutcome.Issued, first.Value.Outcome);
        Assert.Equal(43, Assert.IsType<string>(first.Value.Token).Length);
        Assert.True(replay.IsSuccess, replay.Error.Code);
        Assert.Equal(OrganizationJoinSourceIssuanceOutcome.AlreadyIssued, replay.Value.Outcome);
        Assert.Null(replay.Value.Token);
        Assert.Equal(OrganizationApplicationErrors.JoinSourceIssuanceConflict, changed.Error);
        Assert.Equal(replacementId, first.Value.Source!.InvitationId);
        Assert.Equal(issued.Invitation.InvitationId, first.Value.Source.ReplacesInvitationId);
        Assert.Equal(issued.Invitation.Version, first.Value.Source.ReplacesInvitationVersion);
        Assert.Equal(2, repository.Invitations.Count);
        Assert.Equal(
            DomainInvitationState.Superseded,
            repository.Invitations.Single(item => item.Id == issued.Invitation.InvitationId).Status);
    }

    [Fact]
    public async Task Http_issuance_mapping_preserves_source_context_and_one_time_secret()
    {
        TestRepository repository = CreateRepository();
        Organization organization = Assert.Single(repository.Organizations);
        repository.Memberships.Add(OrganizationMembership.Create(
            Guid.NewGuid(),
            organization.Id,
            "manager",
            DomainMembershipRole.Member,
            "user:owner",
            Guid.NewGuid(),
            Now).Value);
        TestClock clock = new();
        SourceBoundJoinAuthorizationPolicy policy = new();
        using ServiceProvider services = CreateServices(
            repository,
            clock,
            joinSourceAuthorizationPolicy: policy);
        var issue = services.GetRequiredService<ICommandHandler<
            IssueOrganizationInvitationCommand,
            OrganizationJoinSourceIssuance<OrganizationInvitationDto>>>();
        Guid sourceId = Guid.NewGuid();
        IssueOrganizationInvitationCommand command = new(
            new OrganizationInvitationIssuanceRequest(
                sourceId,
                organization.Id,
                null,
                24,
                "manager",
                "user:manager"));

        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> issued =
            await issue.HandleAsync(command, CancellationToken.None);
        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> replayResult =
            await issue.HandleAsync(command, CancellationToken.None);
        Result<OrganizationInvitationIssuanceDto> replay =
            OrganizationEndpointSupport.MapInvitationIssuance(replayResult);

        Assert.True(issued.IsSuccess, issued.Error.Code);
        Assert.True(replay.IsSuccess, replay.Error.Code);
        Assert.Equal(OrganizationJoinSourceIssuanceOutcome.AlreadyIssued, replay.Value.Outcome);
        Assert.Null(replay.Value.Token);
        Assert.Equal(sourceId, replay.Value.Invitation.InvitationId);
        Assert.Collection(
            policy.Contexts,
            context =>
            {
                Assert.Equal(
                    OrganizationJoinSourceAuthorizationOperation.IssueInvitation,
                    context.Operation);
                Assert.Equal(organization.Id, context.OrganizationId);
                Assert.Equal("manager", context.SubjectId);
                Assert.Equal(sourceId, context.SourceId);
            },
            context =>
            {
                Assert.Equal(
                    OrganizationJoinSourceAuthorizationOperation.IssueInvitation,
                    context.Operation);
                Assert.Equal(organization.Id, context.OrganizationId);
                Assert.Equal("manager", context.SubjectId);
                Assert.Equal(sourceId, context.SourceId);
            });
    }

    [Fact]
    public async Task Unbound_invitation_acceptance_is_idempotent_and_secret_is_not_persisted()
    {
        TestRepository repository = CreateRepository();
        TestClock clock = new();
        using ServiceProvider services = CreateServices(repository, clock);
        var accept = services.GetRequiredService<
            ICommandHandler<AcceptOrganizationInvitationCommand, OrganizationInvitationAcceptanceDto>>();
        Organization organization = Assert.Single(repository.Organizations);

        OrganizationInvitationIssuedDto issued = await IssueAsync(
            services, organization, null, null);
        OrganizationInvitation stored = Assert.Single(repository.Invitations);
        Assert.NotEqual(issued.Token, stored.TokenDigest);
        Assert.Equal(43, issued.Token.Length);
        Assert.Equal(64, stored.TokenDigest.Length);

        var first = await accept.HandleAsync(new AcceptOrganizationInvitationCommand(
            issued.Token, "member", "user:member"), CancellationToken.None);
        var retry = await accept.HandleAsync(new AcceptOrganizationInvitationCommand(
            issued.Token, "member", "user:member"), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Single(repository.Memberships, membership => membership.SubjectId == "member");
        Assert.Equal(first.Value.Membership.Membership.MembershipId,
            retry.Value.Membership.Membership.MembershipId);
    }

    [Fact]
    public async Task A_fresh_invitation_does_not_emit_another_acceptance_for_an_active_member()
    {
        TestRepository repository = CreateRepository();
        TestClock clock = new();
        using ServiceProvider services = CreateServices(repository, clock);
        Organization organization = Assert.Single(repository.Organizations);
        repository.Memberships.Add(OrganizationMembership.Create(
            Guid.NewGuid(),
            organization.Id,
            "member",
            DomainMembershipRole.Member,
            "user:owner",
            Guid.NewGuid(),
            Now).Value);
        OrganizationInvitationIssuedDto issued = await IssueAsync(
            services, organization, null, 24);
        var accept = services.GetRequiredService<
            ICommandHandler<AcceptOrganizationInvitationCommand, OrganizationInvitationAcceptanceDto>>();

        Result<OrganizationInvitationAcceptanceDto> result = await accept.HandleAsync(
            new AcceptOrganizationInvitationCommand(
                issued.Token,
                "member",
                "user:member"),
            CancellationToken.None);

        Assert.Equal(OrganizationApplicationErrors.MembershipConflict, result.Error);
        Assert.Equal(DomainInvitationState.Pending, Assert.Single(repository.Invitations).Status);
        Assert.Single(repository.Memberships, membership => membership.SubjectId == "member");
    }

    [Fact]
    public async Task Bound_invitation_fails_closed_without_recipient_verification_extension()
    {
        TestRepository repository = CreateRepository();
        TestClock clock = new();
        using ServiceProvider services = CreateServices(repository, clock);
        var accept = services.GetRequiredService<
            ICommandHandler<AcceptOrganizationInvitationCommand, OrganizationInvitationAcceptanceDto>>();
        Organization organization = Assert.Single(repository.Organizations);
        OrganizationInvitationIssuedDto issued = await IssueAsync(
            services, organization, "member@example.com", 24);

        var result = await accept.HandleAsync(new AcceptOrganizationInvitationCommand(
            issued.Token, "member", "user:member"), CancellationToken.None);

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
        var accept = services.GetRequiredService<
            ICommandHandler<AcceptOrganizationInvitationCommand, OrganizationInvitationAcceptanceDto>>();
        Organization organization = Assert.Single(repository.Organizations);
        OrganizationInvitationIssuedDto issued = await IssueAsync(
            services, organization, null, 24);
        Assert.True((await accept.HandleAsync(new AcceptOrganizationInvitationCommand(
            issued.Token, "first", "user:first"), CancellationToken.None)).IsSuccess);

        var competing = await accept.HandleAsync(new AcceptOrganizationInvitationCommand(
            issued.Token, "second", "user:second"), CancellationToken.None);

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
        var accept = services.GetRequiredService<
            ICommandHandler<AcceptOrganizationInvitationCommand, OrganizationInvitationAcceptanceDto>>();
        Organization organization = Assert.Single(repository.Organizations);
        OrganizationInvitationIssuedDto issued = await IssueAsync(
            services, organization, null, 1);
        clock.UtcNow = Now.AddHours(2);

        var result = await accept.HandleAsync(new AcceptOrganizationInvitationCommand(
            issued.Token, "member", "user:member"), CancellationToken.None);

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
        var accept = services.GetRequiredService<
            ICommandHandler<AcceptOrganizationInvitationCommand, OrganizationInvitationAcceptanceDto>>();
        Organization organization = Assert.Single(repository.Organizations);
        OrganizationInvitationIssuedDto issued = await IssueAsync(
            services, organization, null, 24);
        policy.IsAllowed = false;

        var denied = await accept.HandleAsync(new AcceptOrganizationInvitationCommand(
            issued.Token, "member", "user:member"), CancellationToken.None);
        Assert.True(denied.IsFailure);
        Assert.Equal(OrganizationApplicationErrors.JoinAdmissionRejected, denied.Error);
        Assert.DoesNotContain(repository.Memberships, membership => membership.SubjectId == "member");
        Assert.Null(Assert.Single(repository.Invitations).AcceptedSubjectId);

        policy.IsAllowed = true;
        Assert.True((await accept.HandleAsync(new AcceptOrganizationInvitationCommand(
            issued.Token, "member", "user:member"), CancellationToken.None)).IsSuccess);
        policy.IsAllowed = false;
        var retry = await accept.HandleAsync(new AcceptOrganizationInvitationCommand(
            issued.Token, "member", "user:member"), CancellationToken.None);

        Assert.True(retry.IsSuccess);
        Assert.Equal(2, policy.Contexts.Count);
        OrganizationJoinAdmissionContext context = policy.Contexts[^1];
        Assert.Equal(OrganizationJoinAdmissionOperation.AcceptInvitation, context.Operation);
        Assert.Equal(issued.Invitation.InvitationId, context.SourceId);
        Assert.Equal("member", context.ApplicantSubjectId);
    }

    [Fact]
    public async Task Suspended_organization_blocks_reissue_but_allows_revocation()
    {
        TestRepository repository = CreateRepository();
        TestClock clock = new();
        using ServiceProvider services = CreateServices(repository, clock);
        var reissue = services.GetRequiredService<
            ICommandHandler<
                ReissueOrganizationInvitationCommand,
                OrganizationJoinSourceIssuance<OrganizationInvitationDto>>>();
        var revoke = services.GetRequiredService<
            ICommandHandler<RevokeOrganizationInvitationCommand, OrganizationInvitationDto>>();
        Organization organization = Assert.Single(repository.Organizations);
        OrganizationInvitationIssuedDto issued = await IssueAsync(
            services, organization, null, 24);
        clock.UtcNow = Now.AddMinutes(1);
        Assert.True(organization.Suspend(
            organization.Version, "user:owner", Guid.NewGuid(), clock.UtcNow).IsSuccess);

        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> replacement =
            await reissue.HandleAsync(
            new ReissueOrganizationInvitationCommand(
                organization.Id, issued.Invitation.InvitationId, Guid.NewGuid(), issued.Invitation.Version,
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

    private static async Task<OrganizationInvitationIssuedDto> IssueAsync(
        ServiceProvider services,
        Organization organization,
        string? recipientEmail,
        int? lifetimeHours)
    {
        var handler = services.GetRequiredService<ICommandHandler<
            IssueOrganizationInvitationCommand,
            OrganizationJoinSourceIssuance<OrganizationInvitationDto>>>();
        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> result =
            await handler.HandleAsync(
                new IssueOrganizationInvitationCommand(
                    new OrganizationInvitationIssuanceRequest(
                        Guid.NewGuid(),
                        organization.Id,
                        recipientEmail,
                        lifetimeHours,
                        "owner",
                        "user:owner")),
                CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(OrganizationJoinSourceIssuanceOutcome.Issued, result.Value.Outcome);
        return new OrganizationInvitationIssuedDto(
            result.Value.Source!,
            Assert.IsType<string>(result.Value.Token));
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
        IOrganizationJoinAdmissionPolicy? joinPolicy = null,
        IOrganizationJoinSourceAuthorizationPolicy?
            joinSourceAuthorizationPolicy = null)
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
        services.AddTestOrganizationGovernance();
        if (joinPolicy is not null)
        {
            services.AddSingleton(joinPolicy);
        }
        if (joinSourceAuthorizationPolicy is not null)
        {
            services.AddSingleton(joinSourceAuthorizationPolicy);
        }
        services.AddSingleton<IOrganizationRepository>(repository);
        services.AddSingleton<IOrganizationJoinSourceIssuanceCoordinator>(
            new Gma.Modules.Organizations.Tests.Support.TestOrganizationJoinSourceIssuanceCoordinator(repository));
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

    private sealed class SourceBoundJoinAuthorizationPolicy
        : IOrganizationJoinSourceAuthorizationPolicy
    {
        public List<OrganizationJoinSourceAuthorizationContext> Contexts { get; } = [];

        public ValueTask<OrganizationJoinSourceAuthorizationDecision> EvaluateAsync(
            OrganizationJoinSourceAuthorizationContext context,
            CancellationToken cancellationToken = default)
        {
            this.Contexts.Add(context);
            return ValueTask.FromResult(
                context.SourceId.HasValue
                    ? OrganizationJoinSourceAuthorizationDecision.Allowed
                    : OrganizationJoinSourceAuthorizationDecision.Denied);
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
        public Task<bool> InvitationIdExistsAsync(Guid invitationId, CancellationToken cancellationToken) =>
            Task.FromResult(this.Invitations.Any(item => item.Id == invitationId));
        public Task<OrganizationInvitation?> GetInvitationByDigestAsync(string tokenDigest, CancellationToken cancellationToken) =>
            Task.FromResult(this.Invitations.SingleOrDefault(item => item.TokenDigest == tokenDigest));
        public Task<OrganizationEnrollmentLink?> GetEnrollmentLinkAsync(Guid organizationId, Guid enrollmentLinkId, CancellationToken cancellationToken) => Task.FromResult<OrganizationEnrollmentLink?>(null);
        public Task<bool> EnrollmentLinkIdExistsAsync(Guid enrollmentLinkId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<OrganizationEnrollmentLink?> GetEnrollmentLinkByDigestAsync(string tokenDigest, CancellationToken cancellationToken) => Task.FromResult<OrganizationEnrollmentLink?>(null);
        public Task<OrganizationEnrollmentClaim?> GetEnrollmentClaimAsync(Guid organizationId, Guid claimId, CancellationToken cancellationToken) => Task.FromResult<OrganizationEnrollmentClaim?>(null);
        public Task<OrganizationEnrollmentClaim?> GetEnrollmentClaimBySubjectAsync(Guid enrollmentLinkId, string subjectId, CancellationToken cancellationToken) => Task.FromResult<OrganizationEnrollmentClaim?>(null);
        public Task<bool> HasCurrentPendingEnrollmentClaimAsync(Guid organizationId, string subjectId, DateTimeOffset nowUtc, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> SlugExistsAsync(string slug, Guid? excludingOrganizationId, CancellationToken cancellationToken) => Task.FromResult(false);
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
