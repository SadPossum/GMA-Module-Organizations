namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationMembershipRoleJsonConverter))]
public enum OrganizationMembershipRole
{
    Unknown = 0,
    Member = 1,
    Owner = 2
}
