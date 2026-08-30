namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Handlers;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DomainMembershipRole =
    Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;

[Trait("Category", "Unit")]
public sealed class OrganizationProvisioningTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Provisioning_is_actor_independent_and_replays_current_governance_without_mutation()
    {
        (TestOrganizationRepository repository,
            TestCreationCoordinator coordinator,
            ProvisionOrganizationCommandHandler handler) = CreateHandler();
        Guid organizationId = Guid.NewGuid();
        ProvisionOrganizationCommand command = Command(
            organizationId,
            actorId: "admin:first");

        Result<OrganizationProvisioningResult> created = await handler.HandleAsync(
            command,
            CancellationToken.None);
        Organization organization = repository.Organizations.Single(item =>
            item.Id == organizationId);
        OrganizationMembership membership = repository.Memberships.Single(item =>
            item.OrganizationId == organizationId);
        Assert.True(organization.UpdateProfile(
            "Harbor House Updated",
            "harbor-house-updated",
            expectedVersion: 1,
            "admin:profile",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddMinutes(1)).IsSuccess);
        Assert.True(membership.Suspend(
            membership.Version,
            "admin:governance",
            Guid.NewGuid(),
            Now.AddMinutes(2)).IsSuccess);

        Result<OrganizationProvisioningResult> replayed = await handler.HandleAsync(
            command with { ActorId = "admin:repair" },
            CancellationToken.None);

        Assert.True(created.IsSuccess);
        Assert.Equal(
            OrganizationProvisioningOutcome.Provisioned,
            created.Value.Outcome);
        Assert.True(created.Value.IsSuccess);
        Assert.True(replayed.IsSuccess);
        Assert.Equal(
            OrganizationProvisioningOutcome.AlreadyProvisioned,
            replayed.Value.Outcome);
        Assert.Equal("Harbor House Updated", replayed.Value.Summary!.Organization.Name);
        Assert.Equal(
            OrganizationMembershipStatus.Suspended,
            replayed.Value.Summary.Membership.Status);
        Assert.Equal("admin:first", organization.CreatedBy);
        Assert.Equal("admin:governance", membership.LastChangedBy);
        Assert.Equal(2, repository.Organizations.Count);
        Assert.Equal(2, repository.Memberships.Count);
        Assert.Empty(coordinator.ClosedScopes);
    }

    [Fact]
    public async Task Exact_replay_without_the_original_membership_fails_closed()
    {
        (TestOrganizationRepository repository,
            _,
            ProvisionOrganizationCommandHandler handler) = CreateHandler();
        Guid organizationId = Guid.NewGuid();
        ProvisionOrganizationCommand command = Command(organizationId);
        Assert.True((await handler.HandleAsync(
            command,
            CancellationToken.None)).Value.IsSuccess);
        Assert.Equal(1, repository.Memberships.RemoveAll(membership =>
            membership.OrganizationId == organizationId));

        OrganizationProvisioningResult replay = (await handler.HandleAsync(
            command with { ActorId = "admin:recovery" },
            CancellationToken.None)).Value;

        Assert.Equal(
            OrganizationProvisioningOutcome.IdentityConflict,
            replay.Outcome);
        Assert.Equal(
            OrganizationApplicationErrors.CreationOperationConflict.Code,
            replay.ErrorCode);
        Assert.Null(replay.Summary);
        Assert.Equal(2, repository.Organizations.Count);
        Assert.Single(repository.Memberships);
    }

    [Fact]
    public async Task Changed_identity_slug_collision_cross_channel_and_tombstone_fail_closed()
    {
        (TestOrganizationRepository repository,
            TestCreationCoordinator coordinator,
            ProvisionOrganizationCommandHandler handler) = CreateHandler();
        Guid organizationId = Guid.NewGuid();
        ProvisionOrganizationCommand command = Command(organizationId);
        Assert.True((await handler.HandleAsync(
            command,
            CancellationToken.None)).Value.IsSuccess);

        OrganizationProvisioningResult changed = (await handler.HandleAsync(
            command with { InitialOwnerSubjectId = "owner:other" },
            CancellationToken.None)).Value;
        OrganizationProvisioningResult slugConflict = (await handler.HandleAsync(
            Command(Guid.NewGuid()),
            CancellationToken.None)).Value;

        Guid selfServiceId = Guid.NewGuid();
        Organization selfService = Organization.Create(
            selfServiceId,
            "Self Service House",
            "self-service-house",
            "user:self-owner",
            Guid.NewGuid(),
            Now,
            OrganizationCreationFingerprint.Compute(
                "Self Service House",
                "self-service-house",
                "self-owner",
                "user:self-owner")).Value;
        OrganizationMembership selfServiceOwner = OrganizationMembership.Create(
            Guid.NewGuid(),
            selfServiceId,
            "self-owner",
            DomainMembershipRole.Owner,
            "user:self-owner",
            Guid.NewGuid(),
            Now).Value;
        await repository.AddOrganizationAsync(selfService, CancellationToken.None);
        await repository.AddMembershipAsync(
            selfServiceOwner,
            CancellationToken.None);
        OrganizationProvisioningResult crossChannel = (await handler.HandleAsync(
            new ProvisionOrganizationCommand(
                selfServiceId,
                "Self Service House",
                "self-service-house",
                "self-owner",
                "admin:operator"),
            CancellationToken.None)).Value;

        Guid closedId = Guid.NewGuid();
        coordinator.ClosedScopes.Add(closedId);
        OrganizationProvisioningResult closed = (await handler.HandleAsync(
            Command(closedId) with { Slug = "closed-house" },
            CancellationToken.None)).Value;
        OrganizationProvisioningResult invalid = (await handler.HandleAsync(
            Command(Guid.Empty),
            CancellationToken.None)).Value;

        Assert.Equal(
            OrganizationProvisioningOutcome.IdentityConflict,
            changed.Outcome);
        Assert.Equal(
            OrganizationApplicationErrors.CreationOperationConflict.Code,
            changed.ErrorCode);
        Assert.Equal(
            OrganizationProvisioningOutcome.SlugConflict,
            slugConflict.Outcome);
        Assert.Equal(
            OrganizationProvisioningOutcome.IdentityConflict,
            crossChannel.Outcome);
        Assert.Equal(
            OrganizationProvisioningOutcome.IdentityConflict,
            closed.Outcome);
        Assert.Equal(
            OrganizationProvisioningOutcome.InvalidRequest,
            invalid.Outcome);
        Assert.Equal(3, repository.Organizations.Count);
        Assert.Equal(3, repository.Memberships.Count);
    }

    [Fact]
    public void Provisioning_fingerprint_is_a_frozen_actor_independent_namespace()
    {
        string fingerprint = OrganizationCreationFingerprint.ComputeProvisioning(
            "Harbor House",
            "harbor-house",
            "owner-a");

        Assert.Equal(
            "e99d011f48b8d7b0c07b9f9f23980076279f828052e64a61aba0d2224fb98b8d",
            fingerprint);
        Assert.NotEqual(
            OrganizationCreationFingerprint.Compute(
                "Harbor House",
                "harbor-house",
                "owner-a",
                "admin:first"),
            fingerprint);
    }

    [Fact]
    public void Provisioning_capability_requires_explicit_idempotent_registration()
    {
        ServiceCollection services = new();
        services.AddOrganizationsApplication(new ConfigurationBuilder().Build());

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(IOrganizationProvisioner));

        services.AddOrganizationProvisioning();
        services.AddOrganizationProvisioning();

        Assert.Single(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(IOrganizationProvisioner));
    }

    private static ProvisionOrganizationCommand Command(
        Guid organizationId,
        string actorId = "admin:operator") =>
        new(
            organizationId,
            "Harbor House",
            "harbor-house",
            "owner-a",
            actorId);

    private static (
        TestOrganizationRepository Repository,
        TestCreationCoordinator Coordinator,
        ProvisionOrganizationCommandHandler Handler) CreateHandler()
    {
        Guid seedId = Guid.NewGuid();
        Organization seed = Organization.Create(
            seedId,
            "Seed House",
            "seed-house",
            "admin:seed",
            Guid.NewGuid(),
            Now).Value;
        OrganizationMembership seedOwner = OrganizationMembership.Create(
            Guid.NewGuid(),
            seedId,
            "seed-owner",
            DomainMembershipRole.Owner,
            "admin:seed",
            Guid.NewGuid(),
            Now).Value;
        TestOrganizationRepository repository = new(seed, seedOwner);
        TestCreationCoordinator coordinator = new(repository);
        OrganizationCreationWorkflow workflow = new(
            repository,
            coordinator,
            new TestClock(Now),
            new TestIds());
        return (
            repository,
            coordinator,
            new ProvisionOrganizationCommandHandler(workflow));
    }

    private sealed class TestCreationCoordinator(
        TestOrganizationRepository repository)
        : IOrganizationCreationCoordinator
    {
        public HashSet<Guid> ClosedScopes { get; } = [];

        public async Task<OrganizationCreationAcquisition> AcquireAsync(
            Guid operationId,
            CancellationToken cancellationToken) =>
            new(
                await repository.GetOrganizationAsync(
                    operationId,
                    cancellationToken),
                this.ClosedScopes.Contains(operationId));
    }
}
