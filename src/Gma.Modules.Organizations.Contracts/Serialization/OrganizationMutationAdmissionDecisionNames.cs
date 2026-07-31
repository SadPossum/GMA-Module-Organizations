namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationMutationAdmissionDecisionNames
{
    public static string ToWireName(OrganizationMutationAdmissionDecision decision) =>
        decision switch
        {
            OrganizationMutationAdmissionDecision.Allowed => "allowed",
            OrganizationMutationAdmissionDecision.Denied => "denied",
            OrganizationMutationAdmissionDecision.Unavailable => "unavailable",
            _ => throw new ArgumentOutOfRangeException(
                nameof(decision),
                decision,
                "Organization mutation admission decision is invalid.")
        };

    public static bool TryParse(string? value, out OrganizationMutationAdmissionDecision decision)
    {
        decision = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "allowed" => OrganizationMutationAdmissionDecision.Allowed,
            "denied" => OrganizationMutationAdmissionDecision.Denied,
            "unavailable" => OrganizationMutationAdmissionDecision.Unavailable,
            _ => OrganizationMutationAdmissionDecision.Unknown
        };
        return decision is not OrganizationMutationAdmissionDecision.Unknown;
    }
}

internal sealed class OrganizationMutationAdmissionDecisionJsonConverter
    : JsonConverter<OrganizationMutationAdmissionDecision>
{
    public override OrganizationMutationAdmissionDecision Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationMutationAdmissionDecision>(
            ref reader,
            "Organization mutation admission decision",
            OrganizationMutationAdmissionDecisionNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationMutationAdmissionDecision value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization mutation admission decision",
            OrganizationMutationAdmissionDecisionNames.ToWireName);
}
