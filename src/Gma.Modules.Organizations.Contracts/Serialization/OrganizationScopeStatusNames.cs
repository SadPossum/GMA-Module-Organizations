namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationScopeStatusNames
{
    public static string ToWireName(OrganizationScopeStatus status) =>
        status switch
        {
            OrganizationScopeStatus.Invalid => "invalid",
            OrganizationScopeStatus.Missing => "missing",
            OrganizationScopeStatus.Open => "open",
            OrganizationScopeStatus.Closed => "closed",
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Organization scope status is invalid.")
        };

    public static bool TryParse(string? value, out OrganizationScopeStatus status)
    {
        status = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "invalid" => OrganizationScopeStatus.Invalid,
            "missing" => OrganizationScopeStatus.Missing,
            "open" => OrganizationScopeStatus.Open,
            "closed" => OrganizationScopeStatus.Closed,
            _ => OrganizationScopeStatus.Unknown
        };
        return status is not OrganizationScopeStatus.Unknown;
    }
}

internal sealed class OrganizationScopeStatusJsonConverter
    : JsonConverter<OrganizationScopeStatus>
{
    public override OrganizationScopeStatus Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationScopeStatus>(
            ref reader,
            "Organization scope status",
            OrganizationScopeStatusNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationScopeStatus value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization scope status",
            OrganizationScopeStatusNames.ToWireName);
}
