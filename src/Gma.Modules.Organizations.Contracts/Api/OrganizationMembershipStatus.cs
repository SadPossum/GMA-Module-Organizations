namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationMembershipStatusJsonConverter))]
public enum OrganizationMembershipStatus
{
    Unknown = 0,
    Active = 1,
    Suspended = 2,
    Removed = 3
}
