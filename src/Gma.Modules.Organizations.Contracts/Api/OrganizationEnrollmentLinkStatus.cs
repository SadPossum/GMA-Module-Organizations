namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationEnrollmentLinkStatusJsonConverter))]
public enum OrganizationEnrollmentLinkStatus
{
    Unknown = 0,
    Active = 1,
    Disabled = 2,
    Rotated = 3,
    Expired = 4,
    CapacityReached = 5
}
