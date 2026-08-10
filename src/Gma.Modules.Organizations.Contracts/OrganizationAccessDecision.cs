namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationAccessDecisionJsonConverter))]
public enum OrganizationAccessDecision
{
    Unknown = 0,
    Allowed = 1,
    OrganizationNotFound = 2,
    OrganizationInactive = 3,
    MembershipNotFound = 4,
    MembershipInactive = 5,
    Unavailable = 6
}
