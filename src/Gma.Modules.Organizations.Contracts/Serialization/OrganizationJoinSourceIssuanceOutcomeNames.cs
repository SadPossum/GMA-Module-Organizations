namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationJoinSourceIssuanceOutcomeNames
{
    public static string ToWireName(OrganizationJoinSourceIssuanceOutcome outcome) =>
        outcome switch
        {
            OrganizationJoinSourceIssuanceOutcome.Issued => "issued",
            OrganizationJoinSourceIssuanceOutcome.AlreadyIssued => "already-issued",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Organization join-source issuance outcome is invalid.")
        };

    public static bool TryParse(string? value, out OrganizationJoinSourceIssuanceOutcome outcome)
    {
        outcome = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "issued" => OrganizationJoinSourceIssuanceOutcome.Issued,
            "already-issued" => OrganizationJoinSourceIssuanceOutcome.AlreadyIssued,
            _ => OrganizationJoinSourceIssuanceOutcome.Unknown
        };
        return outcome is not OrganizationJoinSourceIssuanceOutcome.Unknown;
    }
}

internal sealed class OrganizationJoinSourceIssuanceOutcomeJsonConverter
    : JsonConverter<OrganizationJoinSourceIssuanceOutcome>
{
    public override OrganizationJoinSourceIssuanceOutcome Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationJoinSourceIssuanceOutcome>(
            ref reader,
            "Organization join-source issuance outcome",
            OrganizationJoinSourceIssuanceOutcomeNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationJoinSourceIssuanceOutcome value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization join-source issuance outcome",
            OrganizationJoinSourceIssuanceOutcomeNames.ToWireName);
}
