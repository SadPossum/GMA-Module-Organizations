namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationMutationAdmissionOperationJsonConverter))]
public enum OrganizationMutationAdmissionOperation
{
    Unknown = 0,
    UpdateOrganization = 1,
    SuspendOrganization = 2,
    ReactivateOrganization = 3,
    ArchiveOrganization = 4,
    TransferOwnership = 5,
    IssueInvitation = 6,
    ReissueInvitation = 7,
    IssueEnrollmentLink = 8,
    RotateEnrollmentLink = 9
}
