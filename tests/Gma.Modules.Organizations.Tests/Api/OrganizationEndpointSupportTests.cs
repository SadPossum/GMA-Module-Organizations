namespace Gma.Modules.Organizations.Tests;

using System.Security.Claims;
using Gma.Framework.Security;
using Gma.Modules.Organizations.Api;
using Microsoft.AspNetCore.Http;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationEndpointSupportTests
{
    [Theory]
    [InlineData(ApplicationClaimNames.Subject)]
    [InlineData(ClaimTypes.NameIdentifier)]
    public void Subject_can_be_resolved_from_raw_or_mapped_jwt_claim(string claimType)
    {
        DefaultHttpContext context = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(claimType, " member-1 ")],
                authenticationType: "test"))
        };

        bool resolved = OrganizationEndpointSupport.TryGetSubject(context, out string subjectId);

        Assert.True(resolved);
        Assert.Equal("member-1", subjectId);
    }

    [Fact]
    public void Sensitive_organization_responses_disable_caching()
    {
        DefaultHttpContext context = new();

        OrganizationEndpointSupport.SetNoStoreHeaders(context);

        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal("no-cache", context.Response.Headers.Pragma);
    }
}
