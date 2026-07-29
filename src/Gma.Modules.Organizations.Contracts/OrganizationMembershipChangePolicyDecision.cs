namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationMembershipChangePolicyDecisionJsonConverter))]
public enum OrganizationMembershipChangePolicyDecision
{
    Unknown = 0,
    Allowed = 1,
    Denied = 2
}
