namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationInvitationStatusNames
{
    public static string ToWireName(OrganizationInvitationStatus status) =>
        status switch
        {
            OrganizationInvitationStatus.Pending => "pending",
            OrganizationInvitationStatus.Accepted => "accepted",
            OrganizationInvitationStatus.Revoked => "revoked",
            OrganizationInvitationStatus.Superseded => "superseded",
            OrganizationInvitationStatus.Expired => "expired",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Organization invitation status is invalid.")
        };

    public static bool TryParse(string? value, out OrganizationInvitationStatus status)
    {
        status = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "pending" => OrganizationInvitationStatus.Pending,
            "accepted" => OrganizationInvitationStatus.Accepted,
            "revoked" => OrganizationInvitationStatus.Revoked,
            "superseded" => OrganizationInvitationStatus.Superseded,
            "expired" => OrganizationInvitationStatus.Expired,
            _ => OrganizationInvitationStatus.Unknown
        };
        return status is not OrganizationInvitationStatus.Unknown;
    }
}

internal sealed class OrganizationInvitationStatusJsonConverter
    : JsonConverter<OrganizationInvitationStatus>
{
    public override OrganizationInvitationStatus Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationInvitationStatus>(
            ref reader,
            "Organization invitation status",
            OrganizationInvitationStatusNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationInvitationStatus value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization invitation status",
            OrganizationInvitationStatusNames.ToWireName);
}
