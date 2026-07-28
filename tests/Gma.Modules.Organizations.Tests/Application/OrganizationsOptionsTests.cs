namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Modules.Organizations.Application;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationsOptionsTests
{
    [Fact]
    public void Default_claim_lifetime_is_seven_days_and_valid()
    {
        OrganizationsOptions options = new();

        ValidateOptionsResult result = new OrganizationsOptionsValidator()
            .Validate(name: null, options);

        Assert.Equal(168, options.EnrollmentClaimLifetimeHours);
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2_161)]
    public void Claim_lifetime_is_bounded(int lifetimeHours)
    {
        OrganizationsOptions options = new()
        {
            EnrollmentClaimLifetimeHours = lifetimeHours
        };

        ValidateOptionsResult result = new OrganizationsOptionsValidator()
            .Validate(name: null, options);

        Assert.True(result.Failed);
    }
}
