namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationAccessDecisionNames
{
    public static string ToWireName(OrganizationAccessDecision decision) =>
        decision switch
        {
            OrganizationAccessDecision.Allowed => "allowed",
            OrganizationAccessDecision.OrganizationNotFound => "organization-not-found",
            OrganizationAccessDecision.OrganizationInactive => "organization-inactive",
            OrganizationAccessDecision.MembershipNotFound => "membership-not-found",
            OrganizationAccessDecision.MembershipInactive => "membership-inactive",
            OrganizationAccessDecision.Unavailable => "unavailable",
            _ => throw new ArgumentOutOfRangeException(
                nameof(decision),
                decision,
                "Organization access decision is invalid.")
        };

    public static bool TryParse(
        string? value,
        out OrganizationAccessDecision decision)
    {
        decision = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "allowed" => OrganizationAccessDecision.Allowed,
            "organization-not-found" => OrganizationAccessDecision.OrganizationNotFound,
            "organization-inactive" => OrganizationAccessDecision.OrganizationInactive,
            "membership-not-found" => OrganizationAccessDecision.MembershipNotFound,
            "membership-inactive" => OrganizationAccessDecision.MembershipInactive,
            "unavailable" => OrganizationAccessDecision.Unavailable,
            _ => OrganizationAccessDecision.Unknown
        };
        return decision is not OrganizationAccessDecision.Unknown;
    }
}

internal sealed class OrganizationAccessDecisionJsonConverter
    : JsonConverter<OrganizationAccessDecision>
{
    public override OrganizationAccessDecision Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationAccessDecision>(
            ref reader,
            "Organization access decision",
            OrganizationAccessDecisionNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationAccessDecision value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization access decision",
            OrganizationAccessDecisionNames.ToWireName);
}
