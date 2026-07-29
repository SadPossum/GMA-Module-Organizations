namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationEnrollmentClaimStatusNames
{
    public static string ToWireName(OrganizationEnrollmentClaimStatus status) =>
        status switch
        {
            OrganizationEnrollmentClaimStatus.Pending => "pending",
            OrganizationEnrollmentClaimStatus.Accepted => "accepted",
            OrganizationEnrollmentClaimStatus.Rejected => "rejected",
            OrganizationEnrollmentClaimStatus.Expired => "expired",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Organization enrollment claim status is invalid.")
        };

    public static bool TryParse(string? value, out OrganizationEnrollmentClaimStatus status)
    {
        status = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "pending" => OrganizationEnrollmentClaimStatus.Pending,
            "accepted" => OrganizationEnrollmentClaimStatus.Accepted,
            "rejected" => OrganizationEnrollmentClaimStatus.Rejected,
            "expired" => OrganizationEnrollmentClaimStatus.Expired,
            _ => OrganizationEnrollmentClaimStatus.Unknown
        };
        return status is not OrganizationEnrollmentClaimStatus.Unknown;
    }
}

internal sealed class OrganizationEnrollmentClaimStatusJsonConverter
    : JsonConverter<OrganizationEnrollmentClaimStatus>
{
    public override OrganizationEnrollmentClaimStatus Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationEnrollmentClaimStatus>(
            ref reader,
            "Organization enrollment claim status",
            OrganizationEnrollmentClaimStatusNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationEnrollmentClaimStatus value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization enrollment claim status",
            OrganizationEnrollmentClaimStatusNames.ToWireName);
}
