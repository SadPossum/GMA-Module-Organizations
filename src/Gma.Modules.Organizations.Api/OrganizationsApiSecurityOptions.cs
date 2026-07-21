namespace Gma.Modules.Organizations.Api;

using Gma.Framework.Security;

public sealed class OrganizationsApiSecurityOptions
{
    public AuthenticationAssuranceRequirement? GovernanceOperationsAssurance { get; set; }
}
