namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Framework.Cqrs;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DomainMembershipRole = Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;

[Trait("Category", "Unit")]
public sealed class OrganizationGovernanceCoordinationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Profile_update_acquires_shared_governance_before_authorization_reads()
    {
        (TestOrganizationRepository repository, Organization organization, OrganizationMembership owner) =
            CreateRepository(includeMember: false);
        TestOrganizationGovernanceCoordinator governance = new();
        repository.OnGovernanceRead = () => AssertAcquired(
            governance,
            organization.Id,
            TestOrganizationGovernanceMode.Shared);
        using ServiceProvider services = CreateServices(repository, governance);
        var handler = services.GetRequiredService<
            ICommandHandler<UpdateOrganizationCommand, OrganizationDto>>();

        var result = await handler.HandleAsync(
            new UpdateOrganizationCommand(
                organization.Id,
                "Harbor House Updated",
                "harbor-house-updated",
                organization.Version,
                owner.SubjectId,
                "user:owner"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        AssertAcquired(governance, organization.Id, TestOrganizationGovernanceMode.Shared);
    }

    [Fact]
    public async Task Membership_change_acquires_exclusive_governance_before_authorization_reads()
    {
        (TestOrganizationRepository repository, Organization organization, OrganizationMembership owner) =
            CreateRepository(includeMember: true);
        OrganizationMembership member = repository.Memberships.Single(item => item != owner);
        TestOrganizationGovernanceCoordinator governance = new();
        repository.OnGovernanceRead = () => AssertAcquired(
            governance,
            organization.Id,
            TestOrganizationGovernanceMode.Exclusive);
        using ServiceProvider services = CreateServices(repository, governance);
        var handler = services.GetRequiredService<
            ICommandHandler<ChangeOrganizationMembershipCommand, OrganizationMembershipDto>>();

        var result = await handler.HandleAsync(
            new ChangeOrganizationMembershipCommand(
                organization.Id,
                member.SubjectId,
                OrganizationMembershipAction.Suspend,
                organization.Version,
                member.Version,
                owner.SubjectId,
                "user:owner"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        AssertAcquired(governance, organization.Id, TestOrganizationGovernanceMode.Exclusive);
    }

    [Fact]
    public async Task Invalid_self_transfer_is_rejected_before_governance_acquisition()
    {
        (TestOrganizationRepository repository, Organization organization, OrganizationMembership owner) =
            CreateRepository(includeMember: false);
        TestOrganizationGovernanceCoordinator governance = new();
        using ServiceProvider services = CreateServices(repository, governance);
        var handler = services.GetRequiredService<
            ICommandHandler<TransferOrganizationOwnershipCommand, OrganizationMembershipDto>>();

        var result = await handler.HandleAsync(
            new TransferOrganizationOwnershipCommand(
                organization.Id,
                owner.SubjectId,
                organization.Version,
                owner.Version,
                owner.Version,
                owner.SubjectId,
                "user:owner"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(OrganizationApplicationErrors.OwnershipTargetMustDiffer, result.Error);
        Assert.Empty(governance.Acquisitions);
    }

    private static void AssertAcquired(
        TestOrganizationGovernanceCoordinator governance,
        Guid organizationId,
        TestOrganizationGovernanceMode mode)
    {
        (Guid acquiredOrganizationId, TestOrganizationGovernanceMode acquiredMode) =
            Assert.Single(governance.Acquisitions);
        Assert.Equal(organizationId, acquiredOrganizationId);
        Assert.Equal(mode, acquiredMode);
    }

    private static (
        TestOrganizationRepository Repository,
        Organization Organization,
        OrganizationMembership Owner) CreateRepository(bool includeMember)
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
        if (includeMember)
        {
            repository.Memberships.Add(OrganizationMembership.Create(
                Guid.NewGuid(),
                organization.Id,
                "member",
                DomainMembershipRole.Member,
                "user:owner",
                Guid.NewGuid(),
                Now).Value);
        }

        return (repository, organization, owner);
    }

    private static ServiceProvider CreateServices(
        TestOrganizationRepository repository,
        TestOrganizationGovernanceCoordinator governance)
    {
        ServiceCollection services = new();
        services.AddOrganizationsApplication(new ConfigurationBuilder().Build());
        services.AddSingleton<IOrganizationGovernanceCoordinator>(governance);
        services.AddSingleton<IOrganizationRepository>(repository);
        services.AddSingleton<ISystemClock>(new TestClock(Now.AddMinutes(1)));
        services.AddSingleton<IIdGenerator>(new TestIds());
        return services.BuildServiceProvider();
    }
}
