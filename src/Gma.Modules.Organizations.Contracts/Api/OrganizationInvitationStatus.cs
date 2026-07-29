namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationInvitationStatusJsonConverter))]
public enum OrganizationInvitationStatus
{
    Unknown = 0,
    Pending = 1,
    Accepted = 2,
    Revoked = 3,
    Superseded = 4,
    Expired = 5
}
