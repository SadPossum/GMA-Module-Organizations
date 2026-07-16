namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationAccessDecisionReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Reader_fails_closed_for_unavailable_organizations_and_memberships()
    {
        Guid organizationId = Guid.NewGuid();
        Organization organization = Organization.Create(
            organizationId, "Harbor House", "harbor-house", "user:owner", Guid.NewGuid(), Now).Value;
        OrganizationMembership membership = OrganizationMembership.Create(
            Guid.NewGuid(), organizationId, "member-a", OrganizationMembershipRole.Member,
            "user:owner", Guid.NewGuid(), Now).Value;
        TestOrganizationRepository repository = new(organization, membership);
        using ServiceProvider services = CreateServices(repository);
        IOrganizationAccessDecisionReader reader =
            services.GetRequiredService<IOrganizationAccessDecisionReader>();

        Assert.Equal(
            OrganizationAccessDecision.Allowed,
            await reader.ReadAsync(organizationId, "member-a", CancellationToken.None));
        Assert.Equal(
            OrganizationAccessDecision.MembershipNotFound,
            await reader.ReadAsync(organizationId, "missing", CancellationToken.None));

        Assert.True(membership.Suspend(
            membership.Version, "user:owner", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(
            OrganizationAccessDecision.MembershipInactive,
            await reader.ReadAsync(organizationId, "member-a", CancellationToken.None));

        Assert.True(organization.Suspend(
            organization.Version, "user:owner", Guid.NewGuid(), Now.AddMinutes(2)).IsSuccess);
        Assert.Equal(
            OrganizationAccessDecision.OrganizationInactive,
            await reader.ReadAsync(organizationId, "member-a", CancellationToken.None));
        Assert.Equal(
            OrganizationAccessDecision.OrganizationNotFound,
            await reader.ReadAsync(Guid.NewGuid(), "member-a", CancellationToken.None));
    }

    private static ServiceProvider CreateServices(IOrganizationRepository repository)
    {
        ServiceCollection services = new();
        services.AddOrganizationsApplication(new ConfigurationBuilder().Build());
        services.AddSingleton(repository);
        return services.BuildServiceProvider();
    }
}
