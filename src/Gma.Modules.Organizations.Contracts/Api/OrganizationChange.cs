namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationChangeJsonConverter))]
public enum OrganizationChange
{
    Unknown = 0,
    Created = 1,
    ProfileUpdated = 2,
    Suspended = 3,
    Reactivated = 4,
    Archived = 5,
    OwnerCountChanged = 6,
    OwnershipTransferred = 7
}
