namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationCreationAdmissionDecisionNames
{
    public static string ToWireName(OrganizationCreationAdmissionDecision decision) =>
        decision switch
        {
            OrganizationCreationAdmissionDecision.Allowed => "allowed",
            OrganizationCreationAdmissionDecision.Denied => "denied",
            OrganizationCreationAdmissionDecision.SubjectVerificationRequired => "subject-verification-required",
            OrganizationCreationAdmissionDecision.Unavailable => "unavailable",
            _ => throw new ArgumentOutOfRangeException(
                nameof(decision),
                decision,
                "Organization creation admission decision is invalid.")
        };

    public static bool TryParse(
        string? value,
        out OrganizationCreationAdmissionDecision decision)
    {
        decision = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "allowed" => OrganizationCreationAdmissionDecision.Allowed,
            "denied" => OrganizationCreationAdmissionDecision.Denied,
            "subject-verification-required" => OrganizationCreationAdmissionDecision.SubjectVerificationRequired,
            "unavailable" => OrganizationCreationAdmissionDecision.Unavailable,
            _ => OrganizationCreationAdmissionDecision.Unknown
        };
        return decision is not OrganizationCreationAdmissionDecision.Unknown;
    }
}

internal sealed class OrganizationCreationAdmissionDecisionJsonConverter
    : JsonConverter<OrganizationCreationAdmissionDecision>
{
    public override OrganizationCreationAdmissionDecision Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationCreationAdmissionDecision>(
            ref reader,
            "Organization creation admission decision",
            OrganizationCreationAdmissionDecisionNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationCreationAdmissionDecision value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization creation admission decision",
            OrganizationCreationAdmissionDecisionNames.ToWireName);
}
