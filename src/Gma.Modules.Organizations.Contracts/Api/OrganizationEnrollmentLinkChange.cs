namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationEnrollmentLinkChangeJsonConverter))]
public enum OrganizationEnrollmentLinkChange
{
    Unknown = 0,
    Created = 1,
    ClaimReserved = 2,
    ClaimReleased = 3,
    Disabled = 4,
    Rotated = 5
}
