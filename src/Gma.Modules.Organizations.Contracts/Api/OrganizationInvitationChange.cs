namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationInvitationChangeJsonConverter))]
public enum OrganizationInvitationChange
{
    Unknown = 0,
    Created = 1,
    Accepted = 2,
    Revoked = 3,
    Superseded = 4
}
