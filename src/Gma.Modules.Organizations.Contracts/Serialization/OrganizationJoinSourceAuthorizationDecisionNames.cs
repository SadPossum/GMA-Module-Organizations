namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationJoinSourceAuthorizationDecisionNames
{
    public static string ToWireName(
        OrganizationJoinSourceAuthorizationDecision decision) =>
        decision switch
        {
            OrganizationJoinSourceAuthorizationDecision.NotApplicable =>
                "not-applicable",
            OrganizationJoinSourceAuthorizationDecision.Allowed => "allowed",
            OrganizationJoinSourceAuthorizationDecision.Denied => "denied",
            OrganizationJoinSourceAuthorizationDecision.Unavailable =>
                "unavailable",
            _ => throw new ArgumentOutOfRangeException(
                nameof(decision),
                decision,
                "Unknown join-source authorization decisions cannot be serialized.")
        };

    public static bool TryParse(
        string? value,
        out OrganizationJoinSourceAuthorizationDecision decision)
    {
        decision = value switch
        {
            "not-applicable" =>
                OrganizationJoinSourceAuthorizationDecision.NotApplicable,
            "allowed" => OrganizationJoinSourceAuthorizationDecision.Allowed,
            "denied" => OrganizationJoinSourceAuthorizationDecision.Denied,
            "unavailable" =>
                OrganizationJoinSourceAuthorizationDecision.Unavailable,
            _ => OrganizationJoinSourceAuthorizationDecision.Unknown
        };
        return decision != OrganizationJoinSourceAuthorizationDecision.Unknown;
    }
}

internal sealed class OrganizationJoinSourceAuthorizationDecisionJsonConverter
    : JsonConverter<OrganizationJoinSourceAuthorizationDecision>
{
    private const string EnumDisplayName =
        "OrganizationJoinSourceAuthorizationDecision";

    public override OrganizationJoinSourceAuthorizationDecision Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<
            OrganizationJoinSourceAuthorizationDecision>(
            ref reader,
            EnumDisplayName,
            OrganizationJoinSourceAuthorizationDecisionNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationJoinSourceAuthorizationDecision value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            EnumDisplayName,
            OrganizationJoinSourceAuthorizationDecisionNames.ToWireName);
}
