namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationJoinSourceAuthorizationDecisionJsonConverter))]
public enum OrganizationJoinSourceAuthorizationDecision
{
    Unknown = 0,
    NotApplicable = 1,
    Allowed = 2,
    Denied = 3,
    Unavailable = 4
}
