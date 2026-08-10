namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationInvitationRecipientVerificationDecisionJsonConverter))]
public enum OrganizationInvitationRecipientVerificationDecision
{
    Unknown = 0,
    Verified = 1,
    NotVerified = 2,
    Unavailable = 3
}
