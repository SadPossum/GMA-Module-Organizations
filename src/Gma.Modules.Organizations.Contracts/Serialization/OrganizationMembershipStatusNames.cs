namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationMembershipStatusNames
{
    public static string ToWireName(OrganizationMembershipStatus status) =>
        status switch
        {
            OrganizationMembershipStatus.Active => "active",
            OrganizationMembershipStatus.Suspended => "suspended",
            OrganizationMembershipStatus.Removed => "removed",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Organization membership status is invalid.")
        };

    public static bool TryParse(string? value, out OrganizationMembershipStatus status)
    {
        status = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "active" => OrganizationMembershipStatus.Active,
            "suspended" => OrganizationMembershipStatus.Suspended,
            "removed" => OrganizationMembershipStatus.Removed,
            _ => OrganizationMembershipStatus.Unknown
        };
        return status is not OrganizationMembershipStatus.Unknown;
    }
}

internal sealed class OrganizationMembershipStatusJsonConverter
    : JsonConverter<OrganizationMembershipStatus>
{
    public override OrganizationMembershipStatus Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationMembershipStatus>(
            ref reader,
            "Organization membership status",
            OrganizationMembershipStatusNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationMembershipStatus value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization membership status",
            OrganizationMembershipStatusNames.ToWireName);
}
