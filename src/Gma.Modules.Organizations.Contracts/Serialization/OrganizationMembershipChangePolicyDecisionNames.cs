namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationMembershipChangePolicyDecisionNames
{
    public static string ToWireName(OrganizationMembershipChangePolicyDecision decision) =>
        decision switch
        {
            OrganizationMembershipChangePolicyDecision.Allowed => "allowed",
            OrganizationMembershipChangePolicyDecision.Denied => "denied",
            _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Organization membership change policy decision is invalid.")
        };

    public static bool TryParse(string? value, out OrganizationMembershipChangePolicyDecision decision)
    {
        decision = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "allowed" => OrganizationMembershipChangePolicyDecision.Allowed,
            "denied" => OrganizationMembershipChangePolicyDecision.Denied,
            _ => OrganizationMembershipChangePolicyDecision.Unknown
        };
        return decision is not OrganizationMembershipChangePolicyDecision.Unknown;
    }
}

internal sealed class OrganizationMembershipChangePolicyDecisionJsonConverter
    : JsonConverter<OrganizationMembershipChangePolicyDecision>
{
    public override OrganizationMembershipChangePolicyDecision Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationMembershipChangePolicyDecision>(
            ref reader,
            "Organization membership change policy decision",
            OrganizationMembershipChangePolicyDecisionNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationMembershipChangePolicyDecision value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization membership change policy decision",
            OrganizationMembershipChangePolicyDecisionNames.ToWireName);
}
