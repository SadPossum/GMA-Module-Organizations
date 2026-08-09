namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Policies;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DomainEnrollmentLinkState = Gma.Modules.Organizations.Domain.Enums.OrganizationEnrollmentLinkState;
using DomainInvitationState = Gma.Modules.Organizations.Domain.Enums.OrganizationInvitationState;
using DomainMembershipRole = Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;

[Trait("Category", "Unit")]
public sealed class OrganizationMutationAdmissionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task No_product_policy_preserves_default_module_behavior()
    {
        OrganizationMutationAdmissionPolicy admission = new(
            [],
            NullLogger<OrganizationMutationAdmissionPolicy>.Instance);

        Result result = await admission.AuthorizeAsync(
            Context(OrganizationMutationAdmissionOperation.UpdateOrganization),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(
        OrganizationMutationAdmissionDecision.Denied,
        "Organizations.MutationRejected")]
    [InlineData(
        OrganizationMutationAdmissionDecision.Unavailable,
        "Organizations.MutationAdmissionUnavailable")]
    [InlineData(
        OrganizationMutationAdmissionDecision.Unknown,
        "Organizations.MutationAdmissionUnavailable")]
    public async Task Product_decisions_fail_closed_with_stable_errors(
        OrganizationMutationAdmissionDecision decision,
        string expectedErrorCode)
    {
        RecordingMutationPolicy policy = new() { Decision = decision };
        OrganizationMutationAdmissionPolicy admission = new(
            [policy],
            NullLogger<OrganizationMutationAdmissionPolicy>.Instance);

        Result result = await admission.AuthorizeAsync(
            Context(OrganizationMutationAdmissionOperation.UpdateOrganization),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedErrorCode, result.Error.Code);
        Assert.Single(policy.Contexts);
    }

    [Fact]
    public async Task Policy_failure_is_contained_and_fails_closed()
    {
        RecordingMutationPolicy policy = new() { Exception = new InvalidOperationException("offline") };
        OrganizationMutationAdmissionPolicy admission = new(
            [policy],
            NullLogger<OrganizationMutationAdmissionPolicy>.Instance);

        Result result = await admission.AuthorizeAsync(
            Context(OrganizationMutationAdmissionOperation.UpdateOrganization),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            OrganizationApplicationErrors.MutationAdmissionUnavailable,
            result.Error);
    }

    [Fact]
    public async Task Profile_update_denial_preserves_the_organization()
    {
        RecordingMutationPolicy policy = DenyingPolicy();
        using Fixture fixture = CreateFixture(policy);
        long version = fixture.Organization.Version;
        string name = fixture.Organization.Name;
        string slug = fixture.Organization.Slug;
        var handler = fixture.Services.GetRequiredService<
            ICommandHandler<UpdateOrganizationCommand, OrganizationDto>>();

        Result<OrganizationDto> result = await handler.HandleAsync(
            new UpdateOrganizationCommand(
                fixture.Organization.Id,
                "Changed",
                "changed",
                version,
                "owner",
                "user:owner"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(OrganizationApplicationErrors.MutationRejected, result.Error);
        Assert.Equal(version, fixture.Organization.Version);
        Assert.Equal(name, fixture.Organization.Name);
        Assert.Equal(slug, fixture.Organization.Slug);
        Assert.Equal(
            OrganizationMutationAdmissionOperation.UpdateOrganization,
            Assert.Single(policy.Contexts).Operation);
    }

    [Theory]
    [InlineData(
        OrganizationLifecycleAction.Suspend,
        OrganizationMutationAdmissionOperation.SuspendOrganization)]
    [InlineData(
        OrganizationLifecycleAction.Reactivate,
        OrganizationMutationAdmissionOperation.ReactivateOrganization)]
    [InlineData(
        OrganizationLifecycleAction.Archive,
        OrganizationMutationAdmissionOperation.ArchiveOrganization)]
    public async Task Lifecycle_mutations_use_the_exact_operation_and_do_not_change_state_when_denied(
        OrganizationLifecycleAction action,
        OrganizationMutationAdmissionOperation operation)
    {
        RecordingMutationPolicy policy = DenyingPolicy();
        using Fixture fixture = CreateFixture(policy);
        if (action is not OrganizationLifecycleAction.Suspend)
        {
            Assert.True(fixture.Organization.Suspend(
                fixture.Organization.Version,
                "system:setup",
                Guid.NewGuid(),
                Now).IsSuccess);
        }

        long version = fixture.Organization.Version;
        var handler = fixture.Services.GetRequiredService<
            ICommandHandler<ChangeOrganizationLifecycleCommand, OrganizationDto>>();

        Result<OrganizationDto> result = await handler.HandleAsync(
            new ChangeOrganizationLifecycleCommand(
                fixture.Organization.Id,
                action,
                version,
                "owner",
                "user:owner"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(OrganizationApplicationErrors.MutationRejected, result.Error);
        Assert.Equal(version, fixture.Organization.Version);
        Assert.Equal(operation, Assert.Single(policy.Contexts).Operation);
    }

    [Fact]
    public async Task Ownership_transfer_denial_preserves_both_membership_roles()
    {
        RecordingMutationPolicy policy = DenyingPolicy();
        using Fixture fixture = CreateFixture(policy);
        OrganizationMembership target = OrganizationMembership.Create(
            Guid.NewGuid(),
            fixture.Organization.Id,
            "member",
            DomainMembershipRole.Member,
            "user:owner",
            Guid.NewGuid(),
            Now).Value;
        fixture.Repository.Memberships.Add(target);
        OrganizationMembership owner = fixture.Repository.Memberships[0];
        var handler = fixture.Services.GetRequiredService<
            ICommandHandler<TransferOrganizationOwnershipCommand, OrganizationMembershipDto>>();

        Result<OrganizationMembershipDto> result = await handler.HandleAsync(
            new TransferOrganizationOwnershipCommand(
                fixture.Organization.Id,
                target.SubjectId,
                fixture.Organization.Version,
                owner.Version,
                target.Version,
                owner.SubjectId,
                "user:owner"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(OrganizationApplicationErrors.MutationRejected, result.Error);
        Assert.Equal(DomainMembershipRole.Owner, owner.Role);
        Assert.Equal(DomainMembershipRole.Member, target.Role);
        OrganizationMutationAdmissionContext context = Assert.Single(policy.Contexts);
        Assert.Equal(OrganizationMutationAdmissionOperation.TransferOwnership, context.Operation);
        Assert.Equal("member", context.TargetSubjectId);
    }

    [Fact]
    public async Task Invitation_issuance_is_denied_before_persistence()
    {
        RecordingMutationPolicy policy = DenyingPolicy();
        using Fixture fixture = CreateFixture(policy);
        var handler = fixture.Services.GetRequiredService<ICommandHandler<
            IssueOrganizationInvitationCommand,
            OrganizationJoinSourceIssuance<OrganizationInvitationDto>>>();

        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> result =
            await handler.HandleAsync(
                new IssueOrganizationInvitationCommand(
                    new OrganizationInvitationIssuanceRequest(
                        Guid.NewGuid(),
                        fixture.Organization.Id,
                        null,
                        24,
                        "owner",
                        "user:owner")),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(OrganizationApplicationErrors.MutationRejected, result.Error);
        Assert.Empty(fixture.Repository.Invitations);
        OrganizationMutationAdmissionContext context = Assert.Single(policy.Contexts);
        Assert.Equal(OrganizationMutationAdmissionOperation.IssueInvitation, context.Operation);
        Assert.NotNull(context.TargetId);
    }

    [Fact]
    public async Task Invitation_reissue_denial_does_not_supersede_the_existing_source()
    {
        RecordingMutationPolicy policy = new();
        using Fixture fixture = CreateFixture(policy);
        var issue = fixture.Services.GetRequiredService<ICommandHandler<
            IssueOrganizationInvitationCommand,
            OrganizationJoinSourceIssuance<OrganizationInvitationDto>>>();
        var reissue = fixture.Services.GetRequiredService<
            ICommandHandler<
                ReissueOrganizationInvitationCommand,
                OrganizationJoinSourceIssuance<OrganizationInvitationDto>>>();
        OrganizationJoinSourceIssuance<OrganizationInvitationDto> issued =
            (await issue.HandleAsync(
                new IssueOrganizationInvitationCommand(
                    new OrganizationInvitationIssuanceRequest(
                        Guid.NewGuid(),
                        fixture.Organization.Id,
                        null,
                        24,
                        "owner",
                        "user:owner")),
                CancellationToken.None)).Value;
        OrganizationInvitation invitation = Assert.Single(fixture.Repository.Invitations);
        policy.Contexts.Clear();
        policy.Decision = OrganizationMutationAdmissionDecision.Denied;
        Guid replacementId = Guid.NewGuid();
        ReissueOrganizationInvitationCommand command = new(
            fixture.Organization.Id,
            invitation.Id,
            replacementId,
            issued.Source!.Version,
            24,
            "owner",
            "user:owner");

        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> result = await reissue.HandleAsync(
            command,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainInvitationState.Pending, invitation.Status);
        Assert.Single(fixture.Repository.Invitations);
        Assert.DoesNotContain(fixture.Repository.Invitations, item => item.Id == replacementId);
        OrganizationMutationAdmissionContext context = Assert.Single(policy.Contexts);
        Assert.Equal(OrganizationMutationAdmissionOperation.ReissueInvitation, context.Operation);
        Assert.Equal(invitation.Id, context.TargetId);

        policy.Decision = OrganizationMutationAdmissionDecision.Allowed;
        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> retry =
            await reissue.HandleAsync(command, CancellationToken.None);

        Assert.True(retry.IsSuccess, retry.Error.Code);
        Assert.Equal(OrganizationJoinSourceIssuanceOutcome.Issued, retry.Value.Outcome);
        Assert.Equal(replacementId, retry.Value.Source!.InvitationId);
        Assert.Equal(2, fixture.Repository.Invitations.Count);
    }

    [Fact]
    public async Task Exact_reissue_retry_does_not_rerun_product_admission()
    {
        RecordingMutationPolicy policy = new();
        using Fixture fixture = CreateFixture(policy);
        var issue = fixture.Services.GetRequiredService<ICommandHandler<
            IssueOrganizationInvitationCommand,
            OrganizationJoinSourceIssuance<OrganizationInvitationDto>>>();
        var reissue = fixture.Services.GetRequiredService<ICommandHandler<
            ReissueOrganizationInvitationCommand,
            OrganizationJoinSourceIssuance<OrganizationInvitationDto>>>();
        OrganizationJoinSourceIssuance<OrganizationInvitationDto> issued =
            (await issue.HandleAsync(
                new IssueOrganizationInvitationCommand(
                    new OrganizationInvitationIssuanceRequest(
                        Guid.NewGuid(),
                        fixture.Organization.Id,
                        null,
                        24,
                        "owner",
                        "user:owner")),
                CancellationToken.None)).Value;
        Guid replacementId = Guid.NewGuid();
        ReissueOrganizationInvitationCommand command = new(
            fixture.Organization.Id,
            issued.Source!.InvitationId,
            replacementId,
            issued.Source.Version,
            24,
            "owner",
            "user:owner");
        policy.Contexts.Clear();

        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> first =
            await reissue.HandleAsync(command, CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error.Code);
        Assert.Single(policy.Contexts);
        policy.Contexts.Clear();
        policy.Decision = OrganizationMutationAdmissionDecision.Denied;

        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> replay =
            await reissue.HandleAsync(command, CancellationToken.None);

        Assert.True(replay.IsSuccess, replay.Error.Code);
        Assert.Equal(OrganizationJoinSourceIssuanceOutcome.AlreadyIssued, replay.Value.Outcome);
        Assert.Null(replay.Value.Token);
        Assert.Empty(policy.Contexts);
    }

    [Fact]
    public async Task Enrollment_issuance_is_denied_before_persistence()
    {
        RecordingMutationPolicy policy = DenyingPolicy();
        using Fixture fixture = CreateFixture(policy);
        var handler = fixture.Services.GetRequiredService<ICommandHandler<
            IssueOrganizationEnrollmentLinkCommand,
            OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>>>();

        Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>> result =
            await handler.HandleAsync(
                new IssueOrganizationEnrollmentLinkCommand(
                    new OrganizationEnrollmentLinkIssuanceRequest(
                        Guid.NewGuid(),
                        fixture.Organization.Id,
                        24,
                        10,
                        OrganizationEnrollmentApprovalMode.RequiresApproval,
                        "owner",
                        "user:owner")),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(OrganizationApplicationErrors.MutationRejected, result.Error);
        Assert.Empty(fixture.Repository.EnrollmentLinks);
        OrganizationMutationAdmissionContext context = Assert.Single(policy.Contexts);
        Assert.Equal(OrganizationMutationAdmissionOperation.IssueEnrollmentLink, context.Operation);
    }

    [Fact]
    public async Task Enrollment_rotation_denial_preserves_the_source_but_disablement_remains_available()
    {
        RecordingMutationPolicy policy = new();
        using Fixture fixture = CreateFixture(policy);
        var issue = fixture.Services.GetRequiredService<ICommandHandler<
            IssueOrganizationEnrollmentLinkCommand,
            OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>>>();
        var rotate = fixture.Services.GetRequiredService<ICommandHandler<
            RotateOrganizationEnrollmentLinkCommand,
            OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>>>();
        var disable = fixture.Services.GetRequiredService<ICommandHandler<
            DisableOrganizationEnrollmentLinkCommand,
            OrganizationEnrollmentLinkDto>>();
        OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto> issued =
            (await issue.HandleAsync(
                new IssueOrganizationEnrollmentLinkCommand(
                    new OrganizationEnrollmentLinkIssuanceRequest(
                        Guid.NewGuid(),
                        fixture.Organization.Id,
                        24,
                        10,
                        OrganizationEnrollmentApprovalMode.Automatic,
                        "owner",
                        "user:owner")),
                CancellationToken.None)).Value;
        OrganizationEnrollmentLink link = Assert.Single(fixture.Repository.EnrollmentLinks);
        policy.Contexts.Clear();
        policy.Decision = OrganizationMutationAdmissionDecision.Denied;

        Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>> rotation =
            await rotate.HandleAsync(
            new RotateOrganizationEnrollmentLinkCommand(
                fixture.Organization.Id,
                link.Id,
                Guid.NewGuid(),
                issued.Source!.Version,
                24,
                "owner",
                "user:owner"),
            CancellationToken.None);
        Result<OrganizationEnrollmentLinkDto> disablement = await disable.HandleAsync(
            new DisableOrganizationEnrollmentLinkCommand(
                fixture.Organization.Id,
                link.Id,
                issued.Source!.Version,
                "owner",
                "user:owner"),
            CancellationToken.None);

        Assert.True(rotation.IsFailure);
        Assert.True(disablement.IsSuccess);
        Assert.Equal(DomainEnrollmentLinkState.Disabled, link.Status);
        Assert.Single(fixture.Repository.EnrollmentLinks);
        OrganizationMutationAdmissionContext context = Assert.Single(policy.Contexts);
        Assert.Equal(OrganizationMutationAdmissionOperation.RotateEnrollmentLink, context.Operation);
        Assert.Equal(link.Id, context.TargetId);
    }

    private static OrganizationMutationAdmissionContext Context(
        OrganizationMutationAdmissionOperation operation) =>
        new(operation, Guid.NewGuid(), "owner");

    private static RecordingMutationPolicy DenyingPolicy() =>
        new() { Decision = OrganizationMutationAdmissionDecision.Denied };

    private static Fixture CreateFixture(RecordingMutationPolicy policy)
    {
        Organization organization = Organization.Create(
            Guid.NewGuid(),
            "Harbor House",
            "harbor-house",
            "user:owner",
            Guid.NewGuid(),
            Now).Value;
        OrganizationMembership owner = OrganizationMembership.Create(
            Guid.NewGuid(),
            organization.Id,
            "owner",
            DomainMembershipRole.Owner,
            "user:owner",
            Guid.NewGuid(),
            Now).Value;
        TestOrganizationRepository repository = new(organization, owner);
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Organizations:SelfServiceCreationEnabled"] = "true",
                ["Organizations:InvitationDefaultLifetimeHours"] = "168",
                ["Organizations:InvitationMaxLifetimeHours"] = "720",
                ["Organizations:EnrollmentDefaultLifetimeHours"] = "24",
                ["Organizations:EnrollmentMaxLifetimeHours"] = "720",
                ["Organizations:EnrollmentClaimLifetimeHours"] = "168",
                ["Organizations:EnrollmentMaxClaims"] = "1000"
            })
            .Build();
        ServiceCollection services = new();
        services.AddOrganizationsApplication(configuration);
        services.AddTestOrganizationGovernance();
        services.AddSingleton<IOrganizationMutationAdmissionPolicy>(policy);
        services.AddSingleton<IOrganizationRepository>(repository);
        services.AddSingleton<IOrganizationJoinSourceIssuanceCoordinator>(
            new TestOrganizationJoinSourceIssuanceCoordinator(repository));
        services.AddSingleton<ISystemClock>(new TestClock(Now));
        services.AddSingleton<IIdGenerator>(new TestIds());
        return new Fixture(
            repository,
            organization,
            services.BuildServiceProvider());
    }

    private sealed class RecordingMutationPolicy : IOrganizationMutationAdmissionPolicy
    {
        public OrganizationMutationAdmissionDecision Decision { get; set; } =
            OrganizationMutationAdmissionDecision.Allowed;
        public Exception? Exception { get; init; }
        public List<OrganizationMutationAdmissionContext> Contexts { get; } = [];

        public ValueTask<OrganizationMutationAdmissionDecision> EvaluateAsync(
            OrganizationMutationAdmissionContext context,
            CancellationToken cancellationToken = default)
        {
            this.Contexts.Add(context);
            return this.Exception is null
                ? ValueTask.FromResult(this.Decision)
                : ValueTask.FromException<OrganizationMutationAdmissionDecision>(this.Exception);
        }
    }

    private sealed class Fixture(
        TestOrganizationRepository repository,
        Organization organization,
        ServiceProvider services) : IDisposable
    {
        public TestOrganizationRepository Repository { get; } = repository;
        public Organization Organization { get; } = organization;
        public ServiceProvider Services { get; } = services;

        public void Dispose() => this.Services.Dispose();
    }
}
