namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationMembershipChangeJsonConverter))]
public enum OrganizationMembershipChange
{
    Unknown = 0,
    Joined = 1,
    Suspended = 2,
    Resumed = 3,
    Removed = 4,
    PromotedToOwner = 5,
    DemotedToMember = 6,
    Restored = 7
}
