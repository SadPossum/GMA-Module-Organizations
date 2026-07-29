namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationStatusJsonConverter))]
public enum OrganizationStatus
{
    Unknown = 0,
    Active = 1,
    Suspended = 2,
    Archived = 3
}
