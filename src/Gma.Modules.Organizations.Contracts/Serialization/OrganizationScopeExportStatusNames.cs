namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationScopeExportStatusNames
{
    public static string ToWireName(OrganizationScopeExportStatus status) =>
        status switch
        {
            OrganizationScopeExportStatus.Invalid => "invalid",
            OrganizationScopeExportStatus.Completed => "completed",
            OrganizationScopeExportStatus.Missing => "missing",
            OrganizationScopeExportStatus.Closed => "closed",
            OrganizationScopeExportStatus.Stale => "stale",
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Organization scope export status is invalid.")
        };

    public static bool TryParse(
        string? value,
        out OrganizationScopeExportStatus status)
    {
        status = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "invalid" => OrganizationScopeExportStatus.Invalid,
            "completed" => OrganizationScopeExportStatus.Completed,
            "missing" => OrganizationScopeExportStatus.Missing,
            "closed" => OrganizationScopeExportStatus.Closed,
            "stale" => OrganizationScopeExportStatus.Stale,
            _ => OrganizationScopeExportStatus.Unknown
        };
        return status is not OrganizationScopeExportStatus.Unknown;
    }
}

internal sealed class OrganizationScopeExportStatusJsonConverter
    : JsonConverter<OrganizationScopeExportStatus>
{
    public override OrganizationScopeExportStatus Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationScopeExportStatus>(
            ref reader,
            "Organization scope export status",
            OrganizationScopeExportStatusNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationScopeExportStatus value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization scope export status",
            OrganizationScopeExportStatusNames.ToWireName);
}
