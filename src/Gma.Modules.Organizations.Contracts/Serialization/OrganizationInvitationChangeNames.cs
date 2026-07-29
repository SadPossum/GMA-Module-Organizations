namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationInvitationChangeNames
{
    public static string ToWireName(OrganizationInvitationChange change) =>
        change switch
        {
            OrganizationInvitationChange.Created => "created",
            OrganizationInvitationChange.Accepted => "accepted",
            OrganizationInvitationChange.Revoked => "revoked",
            OrganizationInvitationChange.Superseded => "superseded",
            _ => throw new ArgumentOutOfRangeException(nameof(change), change, "Organization invitation change is invalid.")
        };

    public static bool TryParse(string? value, out OrganizationInvitationChange change)
    {
        change = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "created" => OrganizationInvitationChange.Created,
            "accepted" => OrganizationInvitationChange.Accepted,
            "revoked" => OrganizationInvitationChange.Revoked,
            "superseded" => OrganizationInvitationChange.Superseded,
            _ => OrganizationInvitationChange.Unknown
        };
        return change is not OrganizationInvitationChange.Unknown;
    }
}

internal sealed class OrganizationInvitationChangeJsonConverter
    : JsonConverter<OrganizationInvitationChange>
{
    public override OrganizationInvitationChange Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationInvitationChange>(
            ref reader,
            "Organization invitation change",
            OrganizationInvitationChangeNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationInvitationChange value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization invitation change",
            OrganizationInvitationChangeNames.ToWireName);
}
