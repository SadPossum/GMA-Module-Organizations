namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationStatusNames
{
    public static string ToWireName(OrganizationStatus status) =>
        status switch
        {
            OrganizationStatus.Active => "active",
            OrganizationStatus.Suspended => "suspended",
            OrganizationStatus.Archived => "archived",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Organization status is invalid.")
        };

    public static bool TryParse(string? value, out OrganizationStatus status)
    {
        status = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "active" => OrganizationStatus.Active,
            "suspended" => OrganizationStatus.Suspended,
            "archived" => OrganizationStatus.Archived,
            _ => OrganizationStatus.Unknown
        };
        return status is not OrganizationStatus.Unknown;
    }
}

internal sealed class OrganizationStatusJsonConverter : JsonConverter<OrganizationStatus>
{
    public override OrganizationStatus Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationStatus>(
            ref reader,
            "Organization status",
            OrganizationStatusNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationStatus value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization status",
            OrganizationStatusNames.ToWireName);
}
