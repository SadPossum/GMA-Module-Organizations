namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Framework.Cqrs;
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
public sealed class OrganizationAdministrationRecoveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Ensure_owner_is_idempotent_after_promoting_an_active_member()
    {
        TestOrganizationRepository repository = CreateRepository();
        Organization organization = Assert.Single(repository.Organizations);
        OrganizationMembership member = OrganizationMembership.Create(
            Guid.NewGuid(), organization.Id, "recovery-owner", DomainMembershipRole.Member,
            "user:owner", Guid.NewGuid(), Now).Value;
        repository.Memberships.Add(member);
        using ServiceProvider services = CreateServices(repository);
        var handler = services.GetRequiredService<ICommandHandler<
            EnsureOrganizationOwnerForAdministrationCommand, OrganizationMembershipSummaryDto>>();
        var command = new EnsureOrganizationOwnerForAdministrationCommand(
            organization.Id, member.SubjectId, organization.Version, member.Version, "admin:operator");

        var first = await handler.HandleAsync(command, CancellationToken.None);
        var retry = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(OrganizationMembershipRole.Owner, first.Value.Membership.Role);
        Assert.Equal(2, organization.ActiveOwnerCount);
        Assert.Equal(2, member.Version);
    }

    [Fact]
    public async Task Administration_cannot_archive_an_active_organization()
    {
        TestOrganizationRepository repository = CreateRepository();
        Organization organization = Assert.Single(repository.Organizations);
        using ServiceProvider services = CreateServices(repository);
        var handler = services.GetRequiredService<ICommandHandler<
            ChangeOrganizationLifecycleForAdministrationCommand, OrganizationDto>>();

        var result = await handler.HandleAsync(new ChangeOrganizationLifecycleForAdministrationCommand(
            organization.Id, OrganizationLifecycleAction.Archive,
            organization.Version, "admin:operator"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(OrganizationDomainErrors.OrganizationNotSuspended, result.Error);
        Assert.Equal(Gma.Modules.Organizations.Domain.Enums.OrganizationState.Active, organization.Status);
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

    private static ServiceProvider CreateServices(TestOrganizationRepository repository)
    {
        ServiceCollection services = new();
        services.AddOrganizationsApplication(new ConfigurationBuilder().Build());
        services.AddSingleton<IOrganizationRepository>(repository);
        services.AddSingleton<ISystemClock>(new TestClock(Now));
        services.AddSingleton<IIdGenerator>(new TestIds());
        return services.BuildServiceProvider();
    }
}
