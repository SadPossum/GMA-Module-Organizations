namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Framework.ModuleComposition;
using Gma.Modules.Organizations.Contracts;
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
}
