namespace Gma.Modules.Organizations.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(OrganizationJoinAdmissionOperationJsonConverter))]
public enum OrganizationJoinAdmissionOperation
{
    Unknown = 0,
    AcceptInvitation = 1,
    ClaimEnrollment = 2,
    ApproveEnrollment = 3
}
