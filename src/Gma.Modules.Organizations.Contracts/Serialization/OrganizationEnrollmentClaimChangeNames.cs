namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationEnrollmentClaimChangeNames
{
    public static string ToWireName(OrganizationEnrollmentClaimChange change) =>
        change switch
        {
            OrganizationEnrollmentClaimChange.Requested => "requested",
            OrganizationEnrollmentClaimChange.Accepted => "accepted",
            OrganizationEnrollmentClaimChange.Rejected => "rejected",
            _ => throw new ArgumentOutOfRangeException(nameof(change), change, "Organization enrollment claim change is invalid.")
        };

    public static bool TryParse(string? value, out OrganizationEnrollmentClaimChange change)
    {
        change = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "requested" => OrganizationEnrollmentClaimChange.Requested,
            "accepted" => OrganizationEnrollmentClaimChange.Accepted,
            "rejected" => OrganizationEnrollmentClaimChange.Rejected,
            _ => OrganizationEnrollmentClaimChange.Unknown
        };
        return change is not OrganizationEnrollmentClaimChange.Unknown;
    }
}

internal sealed class OrganizationEnrollmentClaimChangeJsonConverter
    : JsonConverter<OrganizationEnrollmentClaimChange>
{
    public override OrganizationEnrollmentClaimChange Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationEnrollmentClaimChange>(
            ref reader,
            "Organization enrollment claim change",
            OrganizationEnrollmentClaimChangeNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationEnrollmentClaimChange value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization enrollment claim change",
            OrganizationEnrollmentClaimChangeNames.ToWireName);
}
