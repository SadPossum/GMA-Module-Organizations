namespace Gma.Modules.Organizations.Tests.Domain;

using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Domain.Errors;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationGovernanceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_establishes_one_active_owner_and_normalized_scope()
    {
        Guid organizationId = Guid.NewGuid();

        var result = Organization.Create(
            organizationId, "  Harbor House  ", "Harbor-House", "user:owner", Guid.NewGuid(), Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(organizationId.ToString("D"), result.Value.ScopeId);
        Assert.Equal("Harbor House", result.Value.Name);
        Assert.Equal("harbor-house", result.Value.Slug);
        Assert.Equal(1, result.Value.ActiveOwnerCount);
        Assert.Equal(OrganizationState.Active, result.Value.Status);
    }

    [Fact]
    public void Last_active_owner_cannot_be_removed()
    {
        Organization organization = CreateOrganization();

        var result = organization.RemoveActiveOwner(
            organization.Version, "user:owner", Guid.NewGuid(), Now.AddMinutes(1));

        Assert.True(result.IsFailure);
        Assert.Equal(OrganizationDomainErrors.LastActiveOwner, result.Error);
        Assert.Equal(1, organization.ActiveOwnerCount);
        Assert.Equal(1, organization.Version);
    }

    [Fact]
    public void Owner_count_mutation_rejects_stale_version()
    {
        Organization organization = CreateOrganization();
        Assert.True(organization.AddActiveOwner(
            organization.Version, "user:owner", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);

        var stale = organization.RemoveActiveOwner(
            expectedVersion: 1, "user:owner", Guid.NewGuid(), Now.AddMinutes(2));

        Assert.True(stale.IsFailure);
        Assert.Equal(OrganizationDomainErrors.VersionConflict, stale.Error);
        Assert.Equal(2, organization.ActiveOwnerCount);
    }

    [Fact]
    public void Membership_lifecycle_preserves_owner_role_for_explicit_resume()
    {
        OrganizationMembership membership = OrganizationMembership.Create(
            Guid.NewGuid(), Guid.NewGuid(), "owner-subject", OrganizationMembershipRole.Owner,
            "user:owner-subject", Guid.NewGuid(), Now).Value;

        Assert.True(membership.Suspend(
            membership.Version, "user:owner-subject", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(OrganizationMembershipRole.Owner, membership.Role);
        Assert.Equal(OrganizationMembershipState.Suspended, membership.Status);
        Assert.True(membership.Resume(
            membership.Version, "user:owner-subject", Guid.NewGuid(), Now.AddMinutes(2)).IsSuccess);
        Assert.Equal(OrganizationMembershipState.Active, membership.Status);
    }

    private static Organization CreateOrganization() => Organization.Create(
        Guid.NewGuid(), "Harbor House", "harbor-house", "user:owner", Guid.NewGuid(), Now).Value;
}
