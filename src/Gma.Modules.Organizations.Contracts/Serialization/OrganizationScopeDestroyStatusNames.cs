namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationScopeDestroyStatusNames
{
    public static string ToWireName(OrganizationScopeDestroyStatus status) =>
        status switch
        {
            OrganizationScopeDestroyStatus.Invalid => "invalid",
            OrganizationScopeDestroyStatus.InProgress => "in-progress",
            OrganizationScopeDestroyStatus.Completed => "completed",
            OrganizationScopeDestroyStatus.Replayed => "replayed",
            OrganizationScopeDestroyStatus.Stale => "stale",
            OrganizationScopeDestroyStatus.Busy => "busy",
            OrganizationScopeDestroyStatus.Conflict => "conflict",
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Organization scope destroy status is invalid.")
        };

    public static bool TryParse(
        string? value,
        out OrganizationScopeDestroyStatus status)
    {
        status = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "invalid" => OrganizationScopeDestroyStatus.Invalid,
            "in-progress" => OrganizationScopeDestroyStatus.InProgress,
            "completed" => OrganizationScopeDestroyStatus.Completed,
            "replayed" => OrganizationScopeDestroyStatus.Replayed,
            "stale" => OrganizationScopeDestroyStatus.Stale,
            "busy" => OrganizationScopeDestroyStatus.Busy,
            "conflict" => OrganizationScopeDestroyStatus.Conflict,
            _ => OrganizationScopeDestroyStatus.Unknown
        };
        return status is not OrganizationScopeDestroyStatus.Unknown;
    }
}

internal sealed class OrganizationScopeDestroyStatusJsonConverter
    : JsonConverter<OrganizationScopeDestroyStatus>
{
    public override OrganizationScopeDestroyStatus Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationScopeDestroyStatus>(
            ref reader,
            "Organization scope destroy status",
            OrganizationScopeDestroyStatusNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationScopeDestroyStatus value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization scope destroy status",
            OrganizationScopeDestroyStatusNames.ToWireName);
}
