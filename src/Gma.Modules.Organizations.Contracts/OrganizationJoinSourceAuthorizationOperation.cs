namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationJoinSourceAuthorizationOperationJsonConverter))]
public enum OrganizationJoinSourceAuthorizationOperation
{
    Unknown = 0,
    ReadInvitations = 1,
    IssueInvitation = 2,
    RevokeInvitation = 3,
    ReissueInvitation = 4,
    ReadEnrollmentLinks = 5,
    IssueEnrollmentLink = 6,
    DisableEnrollmentLink = 7,
    RotateEnrollmentLink = 8,
    ReadJoinRequests = 9,
    ResolveJoinRequest = 10
}
