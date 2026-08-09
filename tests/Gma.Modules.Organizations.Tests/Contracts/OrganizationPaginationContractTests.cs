namespace Gma.Modules.Organizations.Tests.Contracts;

using Gma.Modules.Organizations.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationPaginationContractTests
{
    public static TheoryData<Type> ListResponseTypes => new()
    {
        typeof(OrganizationListResponse),
        typeof(OrganizationCatalogListResponse),
        typeof(OrganizationMemberListResponse),
        typeof(OrganizationInvitationListResponse),
        typeof(OrganizationEnrollmentLinkListResponse),
        typeof(OrganizationJoinRequestListResponse)
    };

    [Theory]
    [MemberData(nameof(ListResponseTypes))]
    public void List_responses_expose_source_compatible_truthful_continuation(Type responseType)
    {
        var property = responseType.GetProperty("HasMore");
        var constructor = Assert.Single(responseType.GetConstructors());
        var parameter = Assert.Single(
            constructor.GetParameters(),
            candidate => string.Equals(candidate.Name, "HasMore", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(property);
        Assert.Equal(typeof(bool), property.PropertyType);
        Assert.True(parameter.HasDefaultValue);
        Assert.Equal(false, parameter.DefaultValue);
    }
}
