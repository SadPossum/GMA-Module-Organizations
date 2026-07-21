namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Framework.ModuleComposition;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationsProfileTests
{
    [Fact]
    public void Default_profile_is_published_by_the_module_descriptor()
    {
        ModuleProfileDescriptor profile = Assert.Single(
            OrganizationsModuleMetadata.Descriptor.GetCompositionProfiles());

        Assert.Equal(OrganizationsProfiles.Default.ProfileName, profile.ProfileName);
    }

    [Fact]
    public void Application_registers_the_membership_lifecycle_contract_once()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();

        services.AddOrganizationsApplication(configuration);
        services.AddOrganizationsApplication(configuration);

        Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IOrganizationMembershipLifecycle));
    }
}
