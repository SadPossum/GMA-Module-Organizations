namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationEnrollmentApprovalModeNames
{
    public static string ToWireName(OrganizationEnrollmentApprovalMode mode) =>
        mode switch
        {
            OrganizationEnrollmentApprovalMode.Automatic => "automatic",
            OrganizationEnrollmentApprovalMode.RequiresApproval => "requires-approval",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Organization enrollment approval mode is invalid.")
        };

    public static bool TryParse(string? value, out OrganizationEnrollmentApprovalMode mode)
    {
        mode = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "automatic" => OrganizationEnrollmentApprovalMode.Automatic,
            "requires-approval" => OrganizationEnrollmentApprovalMode.RequiresApproval,
            _ => OrganizationEnrollmentApprovalMode.Unknown
        };
        return mode is not OrganizationEnrollmentApprovalMode.Unknown;
    }
}

internal sealed class OrganizationEnrollmentApprovalModeJsonConverter
    : JsonConverter<OrganizationEnrollmentApprovalMode>
{
    public override OrganizationEnrollmentApprovalMode Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationEnrollmentApprovalMode>(
            ref reader,
            "Organization enrollment approval mode",
            OrganizationEnrollmentApprovalModeNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationEnrollmentApprovalMode value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization enrollment approval mode",
            OrganizationEnrollmentApprovalModeNames.ToWireName);
}
