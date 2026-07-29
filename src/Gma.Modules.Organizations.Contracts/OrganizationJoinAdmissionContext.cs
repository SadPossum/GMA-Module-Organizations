namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationJoinAdmissionContext(
    OrganizationJoinAdmissionOperation Operation,
    Guid OrganizationId,
    Guid SourceId,
    Guid? ClaimId,
    string ApplicantSubjectId,
    string ActorSubjectId,
    OrganizationEnrollmentApprovalMode? EnrollmentApprovalMode);
