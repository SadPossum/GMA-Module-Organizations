namespace Gma.Modules.Organizations.Contracts;

public interface IOrganizationJoinAdmissionPolicy
{
    ValueTask<bool> IsAllowedAsync(
        OrganizationJoinAdmissionContext context,
        CancellationToken cancellationToken = default);
}

public sealed record OrganizationJoinAdmissionContext(
    OrganizationJoinAdmissionOperation Operation,
    Guid OrganizationId,
    Guid SourceId,
    Guid? ClaimId,
    string ApplicantSubjectId,
    string ActorSubjectId,
    OrganizationEnrollmentApprovalMode? EnrollmentApprovalMode);

public enum OrganizationJoinAdmissionOperation
{
    Unknown = 0,
    AcceptInvitation = 1,
    ClaimEnrollment = 2,
    ApproveEnrollment = 3
}
