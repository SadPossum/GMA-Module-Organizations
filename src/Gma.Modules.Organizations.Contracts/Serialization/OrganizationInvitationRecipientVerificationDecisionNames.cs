namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class OrganizationInvitationRecipientVerificationDecisionNames
{
    public static string ToWireName(
        OrganizationInvitationRecipientVerificationDecision decision) =>
        decision switch
        {
            OrganizationInvitationRecipientVerificationDecision.Verified => "verified",
            OrganizationInvitationRecipientVerificationDecision.NotVerified => "not-verified",
            OrganizationInvitationRecipientVerificationDecision.Unavailable => "unavailable",
            _ => throw new ArgumentOutOfRangeException(
                nameof(decision),
                decision,
                "Organization invitation recipient verification decision is invalid.")
        };

    public static bool TryParse(
        string? value,
        out OrganizationInvitationRecipientVerificationDecision decision)
    {
        decision = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "verified" => OrganizationInvitationRecipientVerificationDecision.Verified,
            "not-verified" => OrganizationInvitationRecipientVerificationDecision.NotVerified,
            "unavailable" => OrganizationInvitationRecipientVerificationDecision.Unavailable,
            _ => OrganizationInvitationRecipientVerificationDecision.Unknown
        };
        return decision is not OrganizationInvitationRecipientVerificationDecision.Unknown;
    }
}

internal sealed class OrganizationInvitationRecipientVerificationDecisionJsonConverter
    : JsonConverter<OrganizationInvitationRecipientVerificationDecision>
{
    public override OrganizationInvitationRecipientVerificationDecision Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.ReadString<OrganizationInvitationRecipientVerificationDecision>(
            ref reader,
            "Organization invitation recipient verification decision",
            OrganizationInvitationRecipientVerificationDecisionNames.TryParse);

    public override void Write(
        Utf8JsonWriter writer,
        OrganizationInvitationRecipientVerificationDecision value,
        JsonSerializerOptions options) =>
        OrganizationContractEnumJson.WriteString(
            writer,
            value,
            "Organization invitation recipient verification decision",
            OrganizationInvitationRecipientVerificationDecisionNames.ToWireName);
}
