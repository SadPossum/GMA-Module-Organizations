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
using Gma.Modules.Organizations.Domain.Errors;
using Gma.Modules.Organizations.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DomainMembershipRole = Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;

[Trait("Category", "Unit")]
public sealed partial class OrganizationEnrollmentFlowTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Contracts_enrollment_issuance_is_idempotent_and_returns_the_secret_only_once()
    {
        TestOrganizationRepository repository = CreateRepository();
        TestClock clock = new(Now);
        using ServiceProvider services = CreateServices(repository, clock);
        var issue = services.GetRequiredService<ICommandHandler<
            IssueOrganizationEnrollmentLinkCommand,
            OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>>>();
        Organization organization = Assert.Single(repository.Organizations);
        Guid sourceId = Guid.NewGuid();
        OrganizationEnrollmentLinkIssuanceRequest request = new(
            sourceId,
            organization.Id,
            24,
            10,
            OrganizationEnrollmentApprovalMode.RequiresApproval,
            "owner",
            "user:owner");

        Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>> first =
            await issue.HandleAsync(new IssueOrganizationEnrollmentLinkCommand(request), CancellationToken.None);
        Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>> replay =
            await issue.HandleAsync(new IssueOrganizationEnrollmentLinkCommand(request), CancellationToken.None);
        Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>> conflict =
            await issue.HandleAsync(
                new IssueOrganizationEnrollmentLinkCommand(request with { MaximumClaims = 11 }),
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
        Assert.True(conflict.IsFailure);
        Assert.Equal(OrganizationApplicationErrors.JoinSourceIssuanceConflict, conflict.Error);
        OrganizationEnrollmentLink stored = Assert.Single(repository.EnrollmentLinks);
        Assert.Equal(sourceId, stored.Id);
        Assert.NotEqual(first.Value.Token, stored.TokenDigest);
    }

    [Fact]
    public async Task Contracts_enrollment_issuance_rejects_a_source_id_owned_by_another_organization()
    {
        TestOrganizationRepository repository = CreateRepository();
        TestClock clock = new(Now);
        using ServiceProvider services = CreateServices(repository, clock);
        var issue = services.GetRequiredService<ICommandHandler<
            IssueOrganizationEnrollmentLinkCommand,
            OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>>>();
        Organization organization = Assert.Single(repository.Organizations);
        Guid sourceId = Guid.NewGuid();
        repository.EnrollmentLinks.Add(OrganizationEnrollmentLink.Create(
            sourceId,
            Guid.NewGuid(),
            "other-owner",
            new string('a', 64),
            Now.AddHours(24),
            10,
            Gma.Modules.Organizations.Domain.Enums.OrganizationEnrollmentApprovalMode.RequiresApproval,
            "user:other-owner",
            Guid.NewGuid(),
            Now).Value);

        Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>> result =
            await issue.HandleAsync(
                new IssueOrganizationEnrollmentLinkCommand(new OrganizationEnrollmentLinkIssuanceRequest(
                    sourceId,
                    organization.Id,
                    24,
                    10,
                    OrganizationEnrollmentApprovalMode.RequiresApproval,
                    "owner",
                    "user:owner")),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(OrganizationApplicationErrors.JoinSourceIssuanceConflict, result.Error);
        Assert.Single(repository.EnrollmentLinks);
    }

    [Fact]
    public void Invitation_and_enrollment_tokens_have_distinct_cryptographic_purposes()
    {
        using ServiceProvider services = CreateServices(CreateRepository(), new TestClock(Now));
        IOrganizationInvitationTokenService invitation =
            services.GetRequiredService<IOrganizationInvitationTokenService>();
        IOrganizationEnrollmentTokenService enrollment =
            services.GetRequiredService<IOrganizationEnrollmentTokenService>();
        const string secret = "abcdefghijklmnopqrstuvwxyzABCDEFGH012345678";

        Assert.True(invitation.IsWellFormed(secret));
        Assert.True(enrollment.IsWellFormed(secret));
        Assert.NotEqual(invitation.ComputeDigest(secret), enrollment.ComputeDigest(secret));
    }

    [Fact]
    public async Task Automatic_claim_is_idempotent_and_reserves_capacity_once()
    {
        TestOrganizationRepository repository = CreateRepository();
        using ServiceProvider services = CreateServices(repository, new TestClock(Now));
        OrganizationEnrollmentLinkIssuedDto issued = await IssueAsync(
            services, repository, OrganizationEnrollmentApprovalMode.Automatic, maximumClaims: 2);
        var claim = services.GetRequiredService<
            ICommandHandler<ClaimOrganizationEnrollmentLinkCommand, OrganizationEnrollmentOutcomeDto>>();

        var first = await claim.HandleAsync(new ClaimOrganizationEnrollmentLinkCommand(
            issued.Token, "member", "user:member"), CancellationToken.None);
        var retry = await claim.HandleAsync(new ClaimOrganizationEnrollmentLinkCommand(
            issued.Token, "member", "user:member"), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(OrganizationEnrollmentClaimStatus.Accepted, first.Value.Claim.Status);
        Assert.Equal(first.Value.Claim.ClaimId, retry.Value.Claim.ClaimId);
        Assert.NotNull(first.Value.Membership);
        Assert.Single(repository.Memberships, item => item.SubjectId == "member");
        Assert.Equal(1, Assert.Single(repository.EnrollmentLinks).ReservedClaims);
    }

    [Fact]
    public async Task Approval_link_creates_no_membership_until_an_owner_approves()
    {
        TestOrganizationRepository repository = CreateRepository();
        using ServiceProvider services = CreateServices(repository, new TestClock(Now));
        OrganizationEnrollmentLinkIssuedDto issued = await IssueAsync(
            services, repository, OrganizationEnrollmentApprovalMode.RequiresApproval, maximumClaims: 2);
        var claimHandler = services.GetRequiredService<
            ICommandHandler<ClaimOrganizationEnrollmentLinkCommand, OrganizationEnrollmentOutcomeDto>>();
        var resolveHandler = services.GetRequiredService<
            ICommandHandler<ResolveOrganizationJoinRequestCommand, OrganizationEnrollmentOutcomeDto>>();

        var pending = await claimHandler.HandleAsync(new ClaimOrganizationEnrollmentLinkCommand(
            issued.Token, "member", "user:member"), CancellationToken.None);

        Assert.True(pending.IsSuccess);
        Assert.Equal(OrganizationEnrollmentClaimStatus.Pending, pending.Value.Claim.Status);
        Assert.Null(pending.Value.Membership);
        Assert.DoesNotContain(repository.Memberships, item => item.SubjectId == "member");

        Organization organization = Assert.Single(repository.Organizations);
        var approved = await resolveHandler.HandleAsync(new ResolveOrganizationJoinRequestCommand(
            organization.Id, pending.Value.Claim.ClaimId, OrganizationJoinRequestDecision.Approve,
            pending.Value.Claim.Version, "owner", "user:owner"), CancellationToken.None);

        Assert.True(approved.IsSuccess);
        Assert.Equal(OrganizationEnrollmentClaimStatus.Accepted, approved.Value.Claim.Status);
        Assert.Equal(OrganizationMembershipRole.Member, approved.Value.Membership!.Membership.Role);
        Assert.Single(repository.Memberships, item => item.SubjectId == "member");
    }

    [Fact]
    public async Task Delegated_resolution_preserves_the_exact_claim_context()
    {
        TestOrganizationRepository repository = CreateRepository();
        Organization organization = Assert.Single(repository.Organizations);
        repository.Memberships.Add(OrganizationMembership.Create(
            Guid.NewGuid(),
            organization.Id,
            "manager",
            DomainMembershipRole.Member,
            "user:owner",
            Guid.NewGuid(),
            Now).Value);
        ClaimBoundJoinAuthorizationPolicy authorizationPolicy = new();
        using ServiceProvider services = CreateServices(
            repository,
            new TestClock(Now),
            joinSourceAuthorizationPolicy: authorizationPolicy);
        OrganizationEnrollmentLinkIssuedDto issued = await IssueAsync(
            services,
            repository,
            OrganizationEnrollmentApprovalMode.RequiresApproval,
            maximumClaims: 1);
        var claimHandler = services.GetRequiredService<
            ICommandHandler<ClaimOrganizationEnrollmentLinkCommand, OrganizationEnrollmentOutcomeDto>>();
        var resolveHandler = services.GetRequiredService<
            ICommandHandler<ResolveOrganizationJoinRequestCommand, OrganizationEnrollmentOutcomeDto>>();
        Result<OrganizationEnrollmentOutcomeDto> pending = await claimHandler.HandleAsync(
            new ClaimOrganizationEnrollmentLinkCommand(
                issued.Token,
                "member",
                "user:member"),
            CancellationToken.None);
        authorizationPolicy.AllowedClaimId = pending.Value.Claim.ClaimId;

        Result<OrganizationEnrollmentOutcomeDto> approved = await resolveHandler.HandleAsync(
            new ResolveOrganizationJoinRequestCommand(
                organization.Id,
                pending.Value.Claim.ClaimId,
                OrganizationJoinRequestDecision.Approve,
                pending.Value.Claim.Version,
                "manager",
                "user:manager"),
            CancellationToken.None);

        Assert.True(approved.IsSuccess, approved.Error.Code);
        OrganizationJoinSourceAuthorizationContext context = Assert.Single(
            authorizationPolicy.Contexts);
        Assert.Equal(
            OrganizationJoinSourceAuthorizationOperation.ResolveJoinRequest,
            context.Operation);
        Assert.Equal(organization.Id, context.OrganizationId);
        Assert.Equal("manager", context.SubjectId);
        Assert.Null(context.SourceId);
        Assert.Equal(pending.Value.Claim.ClaimId, context.ClaimId);
    }

    [Fact]
    public async Task Rejection_releases_capacity_and_does_not_create_a_membership()
    {
        TestOrganizationRepository repository = CreateRepository();
        using ServiceProvider services = CreateServices(repository, new TestClock(Now));
        OrganizationEnrollmentLinkIssuedDto issued = await IssueAsync(
            services, repository, OrganizationEnrollmentApprovalMode.RequiresApproval, maximumClaims: 1);
        var claimHandler = services.GetRequiredService<
            ICommandHandler<ClaimOrganizationEnrollmentLinkCommand, OrganizationEnrollmentOutcomeDto>>();
        var resolveHandler = services.GetRequiredService<
            ICommandHandler<ResolveOrganizationJoinRequestCommand, OrganizationEnrollmentOutcomeDto>>();
        var pending = await claimHandler.HandleAsync(new ClaimOrganizationEnrollmentLinkCommand(
            issued.Token, "first", "user:first"), CancellationToken.None);
        Organization organization = Assert.Single(repository.Organizations);

        var rejected = await resolveHandler.HandleAsync(new ResolveOrganizationJoinRequestCommand(
            organization.Id, pending.Value.Claim.ClaimId, OrganizationJoinRequestDecision.Reject,
            pending.Value.Claim.Version, "owner", "user:owner"), CancellationToken.None);
        var replacement = await claimHandler.HandleAsync(new ClaimOrganizationEnrollmentLinkCommand(
            issued.Token, "second", "user:second"), CancellationToken.None);

        Assert.True(rejected.IsSuccess);
        Assert.Equal(OrganizationEnrollmentClaimStatus.Rejected, rejected.Value.Claim.Status);
        Assert.True(replacement.IsSuccess);
        Assert.Equal(OrganizationEnrollmentClaimStatus.Pending, replacement.Value.Claim.Status);
        Assert.DoesNotContain(repository.Memberships, item => item.SubjectId is "first" or "second");
        Assert.Equal(1, Assert.Single(repository.EnrollmentLinks).ReservedClaims);
    }

    [Fact]
    public async Task Capacity_and_existing_members_cannot_be_used_to_exhaust_a_link()
    {
        TestOrganizationRepository repository = CreateRepository();
        using ServiceProvider services = CreateServices(repository, new TestClock(Now));
        Organization organization = Assert.Single(repository.Organizations);
        repository.Memberships.Add(OrganizationMembership.Create(
            Guid.NewGuid(), organization.Id, "existing", DomainMembershipRole.Member,
            "user:owner", Guid.NewGuid(), Now).Value);
        OrganizationEnrollmentLinkIssuedDto issued = await IssueAsync(
            services, repository, OrganizationEnrollmentApprovalMode.Automatic, maximumClaims: 1);
        var claimHandler = services.GetRequiredService<
            ICommandHandler<ClaimOrganizationEnrollmentLinkCommand, OrganizationEnrollmentOutcomeDto>>();

        var existing = await claimHandler.HandleAsync(new ClaimOrganizationEnrollmentLinkCommand(
            issued.Token, "existing", "user:existing"), CancellationToken.None);
        var first = await claimHandler.HandleAsync(new ClaimOrganizationEnrollmentLinkCommand(
            issued.Token, "first", "user:first"), CancellationToken.None);
        var overCapacity = await claimHandler.HandleAsync(new ClaimOrganizationEnrollmentLinkCommand(
            issued.Token, "second", "user:second"), CancellationToken.None);

        Assert.True(existing.IsFailure);
        Assert.Equal(OrganizationApplicationErrors.MembershipConflict, existing.Error);
        Assert.True(first.IsSuccess);
        Assert.True(overCapacity.IsFailure);
        Assert.Equal(OrganizationDomainErrors.EnrollmentLinkCapacityReached, overCapacity.Error);
        Assert.Equal(1, Assert.Single(repository.EnrollmentLinks).ReservedClaims);
    }

    [Fact]
    public async Task Product_policy_denial_mutates_neither_claim_capacity_nor_membership()
    {
        TestOrganizationRepository repository = CreateRepository();
        RecordingJoinPolicy policy = new() { IsAllowed = false };
        using ServiceProvider services = CreateServices(repository, new TestClock(Now), policy);
        OrganizationEnrollmentLinkIssuedDto issued = await IssueAsync(
            services, repository, OrganizationEnrollmentApprovalMode.Automatic, maximumClaims: 1);
        var claimHandler = services.GetRequiredService<
            ICommandHandler<ClaimOrganizationEnrollmentLinkCommand, OrganizationEnrollmentOutcomeDto>>();

        var denied = await claimHandler.HandleAsync(new ClaimOrganizationEnrollmentLinkCommand(
            issued.Token, "member", "user:member"), CancellationToken.None);

        Assert.True(denied.IsFailure);
        Assert.Equal(OrganizationApplicationErrors.JoinAdmissionRejected, denied.Error);
        Assert.Empty(repository.EnrollmentClaims);
        Assert.DoesNotContain(repository.Memberships, item => item.SubjectId == "member");
        Assert.Equal(0, Assert.Single(repository.EnrollmentLinks).ReservedClaims);
        OrganizationJoinAdmissionContext context = Assert.Single(policy.Contexts);
        Assert.Equal(OrganizationJoinAdmissionOperation.ClaimEnrollment, context.Operation);
        Assert.Equal(issued.EnrollmentLink.EnrollmentLinkId, context.SourceId);
        Assert.Equal("member", context.ApplicantSubjectId);
        Assert.Null(context.ClaimId);
    }

    [Fact]
    public async Task Product_policy_can_allow_a_pending_claim_but_deny_approval()
    {
        TestOrganizationRepository repository = CreateRepository();
        RecordingJoinPolicy policy = new();
        using ServiceProvider services = CreateServices(repository, new TestClock(Now), policy);
        OrganizationEnrollmentLinkIssuedDto issued = await IssueAsync(
            services, repository, OrganizationEnrollmentApprovalMode.RequiresApproval, maximumClaims: 1);
        var claimHandler = services.GetRequiredService<
            ICommandHandler<ClaimOrganizationEnrollmentLinkCommand, OrganizationEnrollmentOutcomeDto>>();
        var resolveHandler = services.GetRequiredService<
            ICommandHandler<ResolveOrganizationJoinRequestCommand, OrganizationEnrollmentOutcomeDto>>();
        var pending = await claimHandler.HandleAsync(new ClaimOrganizationEnrollmentLinkCommand(
            issued.Token, "member", "user:member"), CancellationToken.None);
        policy.IsAllowed = false;

        Organization organization = Assert.Single(repository.Organizations);
        var denied = await resolveHandler.HandleAsync(new ResolveOrganizationJoinRequestCommand(
            organization.Id, pending.Value.Claim.ClaimId, OrganizationJoinRequestDecision.Approve,
            pending.Value.Claim.Version, "owner", "user:owner"), CancellationToken.None);

        Assert.True(denied.IsFailure);
        Assert.Equal(OrganizationApplicationErrors.JoinAdmissionRejected, denied.Error);
        Assert.Equal(
            Gma.Modules.Organizations.Domain.Enums.OrganizationEnrollmentClaimState.Pending,
            Assert.Single(repository.EnrollmentClaims).Status);
        Assert.DoesNotContain(repository.Memberships, item => item.SubjectId == "member");
        OrganizationJoinAdmissionContext context = Assert.Single(
            policy.Contexts, item => item.Operation == OrganizationJoinAdmissionOperation.ApproveEnrollment);
        Assert.Equal(pending.Value.Claim.ClaimId, context.ClaimId);
        Assert.Equal("member", context.ApplicantSubjectId);
        Assert.Equal("owner", context.ActorSubjectId);
    }

    [Fact]
    public async Task Archived_organization_blocks_rotation_but_allows_link_disablement()
    {
        TestOrganizationRepository repository = CreateRepository();
        TestClock clock = new(Now);
        using ServiceProvider services = CreateServices(repository, clock);
        OrganizationEnrollmentLinkIssuedDto issued = await IssueAsync(
            services, repository, OrganizationEnrollmentApprovalMode.Automatic, maximumClaims: 2);
        var change = services.GetRequiredService<
            ICommandHandler<ChangeOrganizationEnrollmentLinkCommand, OrganizationEnrollmentLinkMutationDto>>();
        Organization organization = Assert.Single(repository.Organizations);
        OrganizationEnrollmentLink link = Assert.Single(repository.EnrollmentLinks);
        clock.UtcNow = Now.AddMinutes(1);
        Assert.True(organization.Suspend(
            organization.Version, "user:owner", Guid.NewGuid(), clock.UtcNow).IsSuccess);
        clock.UtcNow = Now.AddMinutes(2);
        Assert.True(organization.Archive(
            organization.Version, "user:owner", Guid.NewGuid(), clock.UtcNow).IsSuccess);

        var replacement = await change.HandleAsync(new ChangeOrganizationEnrollmentLinkCommand(
            organization.Id, link.Id, OrganizationEnrollmentLinkAction.Rotate,
            issued.EnrollmentLink.Version, 24, "owner", "user:owner"), CancellationToken.None);
        var disabled = await change.HandleAsync(new ChangeOrganizationEnrollmentLinkCommand(
            organization.Id, link.Id, OrganizationEnrollmentLinkAction.Disable,
            issued.EnrollmentLink.Version, null, "owner", "user:owner"), CancellationToken.None);

        Assert.True(replacement.IsFailure);
        Assert.Equal(OrganizationDomainErrors.OrganizationNotActive, replacement.Error);
        Assert.True(disabled.IsSuccess);
        Assert.Single(repository.EnrollmentLinks);
        Assert.Null(disabled.Value.ReplacementToken);
        Assert.Equal(OrganizationEnrollmentLinkStatus.Disabled, disabled.Value.EnrollmentLink.Status);
    }

    private static async Task<OrganizationEnrollmentLinkIssuedDto> IssueAsync(
        ServiceProvider services,
        TestOrganizationRepository repository,
        OrganizationEnrollmentApprovalMode mode,
        int maximumClaims)
    {
        var handler = services.GetRequiredService<
            ICommandHandler<CreateOrganizationEnrollmentLinkCommand, OrganizationEnrollmentLinkIssuedDto>>();
        Organization organization = Assert.Single(repository.Organizations);
        var result = await handler.HandleAsync(new CreateOrganizationEnrollmentLinkCommand(
            organization.Id, 24, maximumClaims, mode, "owner", "user:owner"), CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static TestOrganizationRepository CreateRepository()
    {
        Organization organization = Organization.Create(
            Guid.NewGuid(), "Harbor House", "harbor-house",
            "user:owner", Guid.NewGuid(), Now).Value;
        OrganizationMembership owner = OrganizationMembership.Create(
            Guid.NewGuid(), organization.Id, "owner", DomainMembershipRole.Owner,
            "user:owner", Guid.NewGuid(), Now).Value;
        return new TestOrganizationRepository(organization, owner);
    }

    private static ServiceProvider CreateServices(
        TestOrganizationRepository repository,
        TestClock clock,
        IOrganizationJoinAdmissionPolicy? joinPolicy = null,
        IOrganizationJoinSourceAuthorizationPolicy?
            joinSourceAuthorizationPolicy = null)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Organizations:SelfServiceCreationEnabled"] = "true",
                ["Organizations:EnrollmentDefaultLifetimeHours"] = "24",
                ["Organizations:EnrollmentMaxLifetimeHours"] = "720",
                ["Organizations:EnrollmentClaimLifetimeHours"] = "168",
                ["Organizations:EnrollmentMaxClaims"] = "1000"
            })
            .Build();
        ServiceCollection services = new();
        services.AddOrganizationsApplication(configuration);
        if (joinPolicy is not null)
        {
            services.AddSingleton(joinPolicy);
        }
        if (joinSourceAuthorizationPolicy is not null)
        {
            services.AddSingleton(joinSourceAuthorizationPolicy);
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

    private sealed class ClaimBoundJoinAuthorizationPolicy
        : IOrganizationJoinSourceAuthorizationPolicy
    {
        public Guid AllowedClaimId { get; set; }
        public List<OrganizationJoinSourceAuthorizationContext> Contexts { get; } = [];

        public ValueTask<OrganizationJoinSourceAuthorizationDecision> EvaluateAsync(
            OrganizationJoinSourceAuthorizationContext context,
            CancellationToken cancellationToken = default)
        {
            this.Contexts.Add(context);
            return ValueTask.FromResult(
                context.Operation ==
                    OrganizationJoinSourceAuthorizationOperation.ResolveJoinRequest &&
                context.ClaimId == this.AllowedClaimId
                    ? OrganizationJoinSourceAuthorizationDecision.Allowed
                    : OrganizationJoinSourceAuthorizationDecision.Denied);
        }
    }
}
