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
using DomainEnrollmentLinkState =
    Gma.Modules.Organizations.Domain.Enums.OrganizationEnrollmentLinkState;
using DomainEnrollmentApprovalMode =
    Gma.Modules.Organizations.Domain.Enums.OrganizationEnrollmentApprovalMode;
using DomainInvitationState =
    Gma.Modules.Organizations.Domain.Enums.OrganizationInvitationState;
using DomainMembershipRole =
    Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationTerminalJoinSourceRetryTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Exact_terminal_retries_preserve_state_events_and_id_allocation()
    {
        using Fixture fixture = CreateFixture();
        var revoke = fixture.Services.GetRequiredService<ICommandHandler<
            RevokeOrganizationInvitationCommand,
            OrganizationInvitationDto>>();
        var disable = fixture.Services.GetRequiredService<ICommandHandler<
            DisableOrganizationEnrollmentLinkCommand,
            OrganizationEnrollmentLinkDto>>();
        RevokeOrganizationInvitationCommand revokeCommand = new(
            fixture.Organization.Id,
            fixture.Invitation.Id,
            fixture.Invitation.Version,
            "owner",
            "user:owner");
        DisableOrganizationEnrollmentLinkCommand disableCommand = new(
            fixture.Organization.Id,
            fixture.EnrollmentLink.Id,
            fixture.EnrollmentLink.Version,
            "owner",
            "user:owner");

        Result<OrganizationInvitationDto> firstRevocation =
            await revoke.HandleAsync(revokeCommand, CancellationToken.None);
        Result<OrganizationEnrollmentLinkDto> firstDisablement =
            await disable.HandleAsync(disableCommand, CancellationToken.None);
        int invitationEventCount = fixture.Invitation.DomainEvents.Count;
        int linkEventCount = fixture.EnrollmentLink.DomainEvents.Count;
        int generatedIdCount = fixture.Ids.Count;

        Result<OrganizationInvitationDto> revocationReplay =
            await revoke.HandleAsync(revokeCommand, CancellationToken.None);
        Result<OrganizationEnrollmentLinkDto> disablementReplay =
            await disable.HandleAsync(disableCommand, CancellationToken.None);

        Assert.True(firstRevocation.IsSuccess, firstRevocation.Error.Code);
        Assert.True(firstDisablement.IsSuccess, firstDisablement.Error.Code);
        Assert.True(revocationReplay.IsSuccess, revocationReplay.Error.Code);
        Assert.True(disablementReplay.IsSuccess, disablementReplay.Error.Code);
        Assert.Equal(firstRevocation.Value, revocationReplay.Value);
        Assert.Equal(firstDisablement.Value, disablementReplay.Value);
        Assert.Equal(invitationEventCount, fixture.Invitation.DomainEvents.Count);
        Assert.Equal(linkEventCount, fixture.EnrollmentLink.DomainEvents.Count);
        Assert.Equal(generatedIdCount, fixture.Ids.Count);
        Assert.Equal(2, generatedIdCount);
    }

    [Fact]
    public async Task Terminal_replays_still_require_current_management_authority()
    {
        using Fixture fixture = CreateFixture();
        var revoke = fixture.Services.GetRequiredService<ICommandHandler<
            RevokeOrganizationInvitationCommand,
            OrganizationInvitationDto>>();
        var disable = fixture.Services.GetRequiredService<ICommandHandler<
            DisableOrganizationEnrollmentLinkCommand,
            OrganizationEnrollmentLinkDto>>();
        RevokeOrganizationInvitationCommand revokeCommand = new(
            fixture.Organization.Id,
            fixture.Invitation.Id,
            fixture.Invitation.Version,
            "owner",
            "user:owner");
        DisableOrganizationEnrollmentLinkCommand disableCommand = new(
            fixture.Organization.Id,
            fixture.EnrollmentLink.Id,
            fixture.EnrollmentLink.Version,
            "owner",
            "user:owner");
        Assert.True((await revoke.HandleAsync(
            revokeCommand,
            CancellationToken.None)).IsSuccess);
        Assert.True((await disable.HandleAsync(
            disableCommand,
            CancellationToken.None)).IsSuccess);
        int generatedIdCount = fixture.Ids.Count;
        Assert.True(fixture.Owner.Suspend(
            fixture.Owner.Version,
            "system:security",
            Guid.NewGuid(),
            Now.AddMinutes(1)).IsSuccess);

        Result<OrganizationInvitationDto> revocationReplay =
            await revoke.HandleAsync(revokeCommand, CancellationToken.None);
        Result<OrganizationEnrollmentLinkDto> disablementReplay =
            await disable.HandleAsync(disableCommand, CancellationToken.None);

        Assert.Equal(OrganizationApplicationErrors.MembershipRequired, revocationReplay.Error);
        Assert.Equal(OrganizationApplicationErrors.MembershipRequired, disablementReplay.Error);
        Assert.Equal(generatedIdCount, fixture.Ids.Count);
    }

    [Fact]
    public void Replay_proof_rejects_wrong_actor_version_and_terminal_kind()
    {
        using Fixture fixture = CreateFixture();
        long invitationVersion = fixture.Invitation.Version;
        long linkVersion = fixture.EnrollmentLink.Version;
        Assert.True(fixture.Invitation.Revoke(
            invitationVersion,
            "user:owner",
            Guid.NewGuid(),
            Now).IsSuccess);
        Assert.True(fixture.EnrollmentLink.Disable(
            linkVersion,
            "user:owner",
            Guid.NewGuid(),
            Now).IsSuccess);

        Assert.True(fixture.Invitation.IsExactRevocationReplay(
            invitationVersion,
            " user:owner "));
        Assert.True(fixture.EnrollmentLink.IsExactDisableReplay(
            linkVersion,
            " user:owner "));
        Assert.False(fixture.Invitation.IsExactRevocationReplay(
            invitationVersion,
            "user:other"));
        Assert.False(fixture.EnrollmentLink.IsExactDisableReplay(
            linkVersion,
            "user:other"));
        Assert.False(fixture.Invitation.IsExactRevocationReplay(
            invitationVersion - 1,
            "user:owner"));
        Assert.False(fixture.EnrollmentLink.IsExactDisableReplay(
            linkVersion - 1,
            "user:owner"));

        OrganizationInvitation superseded = CreateInvitation(fixture.Organization.Id);
        Assert.True(superseded.Supersede(
            superseded.Version,
            "user:owner",
            Guid.NewGuid(),
            Now).IsSuccess);
        OrganizationEnrollmentLink rotated = CreateEnrollmentLink(fixture.Organization.Id);
        Assert.True(rotated.Rotate(
            rotated.Version,
            "user:owner",
            Guid.NewGuid(),
            Now).IsSuccess);
        Assert.Equal(DomainInvitationState.Superseded, superseded.Status);
        Assert.Equal(DomainEnrollmentLinkState.Rotated, rotated.Status);
        Assert.False(superseded.IsExactRevocationReplay(1, "user:owner"));
        Assert.False(rotated.IsExactDisableReplay(1, "user:owner"));
    }

    private static Fixture CreateFixture()
    {
        Organization organization = Organization.Create(
            Guid.NewGuid(),
            "Retry House",
            "retry-house",
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
        OrganizationInvitation invitation = CreateInvitation(organization.Id);
        OrganizationEnrollmentLink enrollmentLink = CreateEnrollmentLink(organization.Id);
        TestOrganizationRepository repository = new(organization, owner);
        repository.Invitations.Add(invitation);
        repository.EnrollmentLinks.Add(enrollmentLink);
        CountingIds ids = new();
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
        services.AddSingleton<IOrganizationRepository>(repository);
        services.AddSingleton<IOrganizationJoinSourceIssuanceCoordinator>(
            new TestOrganizationJoinSourceIssuanceCoordinator(repository));
        services.AddSingleton<ISystemClock>(new TestClock(Now));
        services.AddSingleton<IIdGenerator>(ids);
        return new Fixture(
            organization,
            owner,
            invitation,
            enrollmentLink,
            ids,
            services.BuildServiceProvider());
    }

    private static OrganizationInvitation CreateInvitation(Guid organizationId) =>
        OrganizationInvitation.Create(
            Guid.NewGuid(),
            organizationId,
            "owner",
            "guest@example.test",
            new string('a', OrganizationInvitation.TokenDigestLength),
            Now.AddDays(1),
            "user:owner",
            Guid.NewGuid(),
            Now).Value;

    private static OrganizationEnrollmentLink CreateEnrollmentLink(Guid organizationId) =>
        OrganizationEnrollmentLink.Create(
            Guid.NewGuid(),
            organizationId,
            "owner",
            new string('b', OrganizationEnrollmentLink.TokenDigestLength),
            Now.AddDays(1),
            10,
            DomainEnrollmentApprovalMode.Automatic,
            "user:owner",
            Guid.NewGuid(),
            Now).Value;

    private sealed class CountingIds : IIdGenerator
    {
        public int Count { get; private set; }

        public Guid NewId()
        {
            this.Count++;
            return Guid.CreateVersion7();
        }
    }

    private sealed record Fixture(
        Organization Organization,
        OrganizationMembership Owner,
        OrganizationInvitation Invitation,
        OrganizationEnrollmentLink EnrollmentLink,
        CountingIds Ids,
        ServiceProvider Services) : IDisposable
    {
        public void Dispose() => this.Services.Dispose();
    }
}
