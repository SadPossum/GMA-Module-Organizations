namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationEnrollmentLinkChangeNames
{
    public static string ToWireName(OrganizationEnrollmentLinkChange change) =>
        change switch
        {
            OrganizationEnrollmentLinkChange.Created => "created",
            OrganizationEnrollmentLinkChange.ClaimReserved => "claim-reserved",
            OrganizationEnrollmentLinkChange.ClaimReleased => "claim-released",
            OrganizationEnrollmentLinkChange.Disabled => "disabled",
            OrganizationEnrollmentLinkChange.Rotated => "rotated",
            _ => throw new ArgumentOutOfRangeException(nameof(change), change, "Organization enrollment link change is invalid.")
        };

    public static bool TryParse(string? value, out OrganizationEnrollmentLinkChange change)
    {
        change = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "created" => OrganizationEnrollmentLinkChange.Created,
            "claim-reserved" => OrganizationEnrollmentLinkChange.ClaimReserved,
            "claim-released" => OrganizationEnrollmentLinkChange.ClaimReleased,
            "disabled" => OrganizationEnrollmentLinkChange.Disabled,
            "rotated" => OrganizationEnrollmentLinkChange.Rotated,
            _ => OrganizationEnrollmentLinkChange.Unknown
        };
        return change is not OrganizationEnrollmentLinkChange.Unknown;
    }
}

internal sealed class OrganizationEnrollmentLinkChangeJsonConverter
    : JsonConverter<OrganizationEnrollmentLinkChange>
{
    public override OrganizationEnrollmentLinkChange Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationEnrollmentLinkChange>(
            ref reader,
            "Organization enrollment link change",
            OrganizationEnrollmentLinkChangeNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationEnrollmentLinkChange value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization enrollment link change",
            OrganizationEnrollmentLinkChangeNames.ToWireName);
}
