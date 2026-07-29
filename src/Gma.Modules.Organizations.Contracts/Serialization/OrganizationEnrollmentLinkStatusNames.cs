namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationEnrollmentLinkStatusNames
{
    public static string ToWireName(OrganizationEnrollmentLinkStatus status) =>
        status switch
        {
            OrganizationEnrollmentLinkStatus.Active => "active",
            OrganizationEnrollmentLinkStatus.Disabled => "disabled",
            OrganizationEnrollmentLinkStatus.Rotated => "rotated",
            OrganizationEnrollmentLinkStatus.Expired => "expired",
            OrganizationEnrollmentLinkStatus.CapacityReached => "capacity-reached",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Organization enrollment link status is invalid.")
        };

    public static bool TryParse(string? value, out OrganizationEnrollmentLinkStatus status)
    {
        status = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "active" => OrganizationEnrollmentLinkStatus.Active,
            "disabled" => OrganizationEnrollmentLinkStatus.Disabled,
            "rotated" => OrganizationEnrollmentLinkStatus.Rotated,
            "expired" => OrganizationEnrollmentLinkStatus.Expired,
            "capacity-reached" => OrganizationEnrollmentLinkStatus.CapacityReached,
            _ => OrganizationEnrollmentLinkStatus.Unknown
        };
        return status is not OrganizationEnrollmentLinkStatus.Unknown;
    }
}

internal sealed class OrganizationEnrollmentLinkStatusJsonConverter
    : JsonConverter<OrganizationEnrollmentLinkStatus>
{
    public override OrganizationEnrollmentLinkStatus Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationEnrollmentLinkStatus>(
            ref reader,
            "Organization enrollment link status",
            OrganizationEnrollmentLinkStatusNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationEnrollmentLinkStatus value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization enrollment link status",
            OrganizationEnrollmentLinkStatusNames.ToWireName);
}
