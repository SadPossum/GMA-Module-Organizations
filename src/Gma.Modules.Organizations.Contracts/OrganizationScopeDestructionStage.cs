namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationScopeDestructionStageJsonConverter))]
public enum OrganizationScopeDestructionStage
{
    Unknown = 0,
    InboxMessages = 1,
    OutboxMessages = 2,
    EnrollmentClaims = 3,
    Invitations = 4,
    EnrollmentLinks = 5,
    Memberships = 6,
    Organization = 7,
    Completed = 8
}
