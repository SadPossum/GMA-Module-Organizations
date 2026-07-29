namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationMembershipRoleNames
{
    public static string ToWireName(OrganizationMembershipRole role) =>
        role switch
        {
            OrganizationMembershipRole.Member => "member",
            OrganizationMembershipRole.Owner => "owner",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Organization membership role is invalid.")
        };

    public static bool TryParse(string? value, out OrganizationMembershipRole role)
    {
        role = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "member" => OrganizationMembershipRole.Member,
            "owner" => OrganizationMembershipRole.Owner,
            _ => OrganizationMembershipRole.Unknown
        };
        return role is not OrganizationMembershipRole.Unknown;
    }
}

internal sealed class OrganizationMembershipRoleJsonConverter
    : JsonConverter<OrganizationMembershipRole>
{
    public override OrganizationMembershipRole Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationMembershipRole>(
            ref reader,
            "Organization membership role",
            OrganizationMembershipRoleNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationMembershipRole value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization membership role",
            OrganizationMembershipRoleNames.ToWireName);
}
