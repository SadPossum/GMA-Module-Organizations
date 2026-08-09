namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationEnrollmentLinkIssuanceRequest(
    Guid SourceId,
    Guid OrganizationId,
    int? LifetimeHours,
    int MaximumClaims,
    OrganizationEnrollmentApprovalMode ApprovalMode,
    string SubjectId,
    string ActorId);
