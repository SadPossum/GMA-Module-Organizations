namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationJoinAdmissionDecisionNames
{
    public static string ToWireName(OrganizationJoinAdmissionDecision decision) =>
        decision switch
        {
            OrganizationJoinAdmissionDecision.Allowed => "allowed",
            OrganizationJoinAdmissionDecision.Denied => "denied",
            OrganizationJoinAdmissionDecision.Unavailable => "unavailable",
            _ => throw new ArgumentOutOfRangeException(
                nameof(decision),
                decision,
                "Organization join admission decision is invalid.")
        };

    public static bool TryParse(
        string? value,
        out OrganizationJoinAdmissionDecision decision)
    {
        decision = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "allowed" => OrganizationJoinAdmissionDecision.Allowed,
            "denied" => OrganizationJoinAdmissionDecision.Denied,
            "unavailable" => OrganizationJoinAdmissionDecision.Unavailable,
            _ => OrganizationJoinAdmissionDecision.Unknown
        };
        return decision is not OrganizationJoinAdmissionDecision.Unknown;
    }
}

internal sealed class OrganizationJoinAdmissionDecisionJsonConverter
    : JsonConverter<OrganizationJoinAdmissionDecision>
{
    public override OrganizationJoinAdmissionDecision Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationJoinAdmissionDecision>(
            ref reader,
            "Organization join admission decision",
            OrganizationJoinAdmissionDecisionNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationJoinAdmissionDecision value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization join admission decision",
            OrganizationJoinAdmissionDecisionNames.ToWireName);
}
